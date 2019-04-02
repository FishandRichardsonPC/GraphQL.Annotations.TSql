using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GraphQL.Annotations.TSql.Generators;
using GraphQL.Annotations.TSql.ParameterTypes;
using GraphQL.Annotations.TSql.GraphTypes;
using GraphQL.Annotations.TSql.Query;
using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;
using Newtonsoft.Json;

namespace GraphQL.Annotations.TSql
{
    public interface ISqlConnectionGetter
    {
        SqlConnection GetConnection(ResolveFieldContext context);
    }

	public interface ISqlFieldResolver
	{
		BatchItem GetBatch(
			IDictionary<string, Field> ipSubFields,
			IDictionary<string, object> arguments,
			ResolveFieldContext context,
			PropertyInfo srcProperty = null
		);

		IEnumerable<string> GetSelect(
			BatchItem batch,
			string joinTo,
			bool includeIds = true,
			bool isFirst = true,
			IEnumerable<DbField> fields = null,
			bool includeAll = false
		);
	}

	internal static class SqlFieldResolverData
	{
		internal static Dictionary<Type, Type> TypeMap = new Dictionary<Type, Type>
		{
			{typeof(StringGraphType), typeof(SqlStringParameter)},
			{typeof(string), typeof(SqlStringParameter)},
			{typeof(IntGraphType), typeof(SqlIntParameter)},
			{typeof(int?), typeof(SqlIntParameter)},
			{typeof(int), typeof(SqlIntParameter)},
			{typeof(DateOnlyGraphType), typeof(SqlDateParameter)},
			{typeof(DateGraphType), typeof(SqlDateTimeParameter)},
			{typeof(DateTime?), typeof(SqlDateTimeParameter)},
			{typeof(DateTime), typeof(SqlDateTimeParameter)},
			{typeof(TimeSpan?), typeof(IsoTimeSpanGraphType)},
			{typeof(TimeSpan), typeof(IsoTimeSpanGraphType)}
		};

		internal static readonly LazyDictionary<Type, ISqlFieldResolver> Resolvers = new LazyDictionary<Type, ISqlFieldResolver>((t) => (ISqlFieldResolver)Activator.CreateInstance(t));
		internal static readonly LazyDictionary<Type, ISqlParameterType> Parameters = new LazyDictionary<Type, ISqlParameterType>((t) => (ISqlParameterType)Activator.CreateInstance(t));
	}

	public abstract class SqlFieldResolver<T> : ObjectGraphType<T>,
		IFieldResolver,
		ISqlFieldResolver,
		ICountResolver
		where T : SqlFieldResolver<T>, new()
	{
	    [SqlGraphQLField(
	        DbFieldName = "",
	        Transform = "count(*)",
	        IsAggregation = true,
	        Description = "Count of matching records, will cause the query to be grouped by all requested fields unless the _groupBy parameter is provided"
        )]
	    // ReSharper disable once InconsistentNaming - The _ notation is to avoid naming conflicts with non system fields
	    public int? _count { get; set; }

		public abstract string Table { get; }
		public abstract string DefaultOrder { get; }
		public abstract string PrimaryProperty { get; }

		public BatchItem LastBatchItem { get; private set; }

		private const BindingFlags BindingFlags = System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

		private static ConcurrentDictionary<Type, ConcurrentDictionary<string, IFieldType>> fieldCache = new ConcurrentDictionary<Type, ConcurrentDictionary<string, IFieldType>>();

		public SqlFieldResolver()
		{
			var type = this.GetType();

		    if (!SqlFieldResolver<T>.fieldCache.ContainsKey(type))
		    {
		        SqlFieldResolver<T>.fieldCache[type] = new ConcurrentDictionary<string, IFieldType>();
		        foreach (var method in type
		            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
		            .Where(m => !m.IsSpecialName))
		        {
		            var funcAttr = method.GetCustomAttribute<SqlGraphQLFuncAttribute>();

		            if (funcAttr != null)
		            {
		                var name = funcAttr.Name ?? Utils.FirstCharacterToLower(method.Name);
		                var field = this.Fields.First(v => v.Name == name);
		                if (funcAttr.ForwardTypeArguments)
		                {
			                var args = Utils.GetArgumentsForType(this.GetSqlFieldType(method.ReturnType),false);
			                foreach (var queryArgument in args)
			                {
				                field.Arguments.Add(queryArgument);
			                }
		                }

		                SqlFieldResolver<T>.fieldCache[type][name] = field;
		            }
		        }

		        foreach (var prop in type.GetProperties(
		            BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance))
		        {
		            var fieldAttr = prop.GetCustomAttribute<SqlGraphQLRelatedAttribute>();
		            if (fieldAttr == null)
		            {
		                continue;
		            }

		            var name = fieldAttr.Name ?? Utils.FirstCharacterToLower(prop.Name);
		            var field = this.Fields.FirstOrDefault(v => v.Name == name);
		            if (field == null)
		            {
		                throw new ArgumentException($"Could not find field for {name}");
		            }

		            field.Arguments = new QueryArguments(
		                Utils.GetArgumentsForType(this.GetSqlFieldType(prop.PropertyType), false));

		            SqlFieldResolver<T>.fieldCache[type][name] = field;
		        }
		    }
		    else
		    {
		        foreach (var param in SqlFieldResolver<T>.fieldCache[type])
		        {
                    var name = param.Key;
                    var value = param.Value;
		            var field = this.Fields.FirstOrDefault(v => v.Name == name);
		            if (field != null)
		            {
		                field.Arguments = value.Arguments;
		            }
		        }
		    }
		}

		private Type GetSqlFieldType(Type returnType)
		{
			while (
				returnType.IsGenericType
				&& !returnType
					.IsSubclassOf(
						typeof(SqlFieldResolver<>).MakeGenericType(
							returnType.GenericTypeArguments[0]
						)
					)
			)
			{
				returnType = returnType.GenericTypeArguments[0];
			}

			return returnType;
		}

        public DbField? GetDbFieldFromName(string propertyName, bool isIdentifierOnly = false)
        {
            return this.GetDbFieldFromProperty(
                this.GetType().GetProperty(propertyName, SqlFieldResolver<T>.BindingFlags),
                isIdentifierOnly
            );
        }

		private DbField? GetDbFieldFromProperty(PropertyInfo prop, bool isIdentifierOnly = false)
		{
            if (prop == null)
            {
                return null;
            }

			var fieldAttr =
				(ISqlFieldAttribute) prop.GetCustomAttribute<SqlGraphQLFieldAttribute>()
				?? prop.GetCustomAttribute<SqlFieldAttribute>();
			if (fieldAttr == null)
			{
				var sqlAttr = prop.GetCustomAttribute<SqlGraphQLRelatedAttribute>();
				if (sqlAttr?.ForeignProperty != null)
				{
					var foreignProp = prop.ReflectedType?.GetProperty(sqlAttr.LocalProperty, SqlFieldResolver<T>.BindingFlags);
					if (foreignProp == null)
					{
						throw new ArgumentException(
							$"Invalid LocalProperty set on {prop.DeclaringType?.Name}.{prop.Name}");
					}

					return this.GetDbFieldFromProperty(foreignProp, true);
				}
				else if (sqlAttr == null)
				{
					return new DbField
					{
						Field = prop.Name,
						Type = prop.PropertyType,
					    IsIdentifierOnly = isIdentifierOnly
					};
				}
				else
				{
					return null;
				}
			}

			if (fieldAttr.DbFieldName != null)
			{
				return new DbField
				{
					Field = fieldAttr.DbFieldName,
					Type = prop.PropertyType,
					GraphType = fieldAttr.ReturnType,
					ReverseTransform = fieldAttr.ReverseTransform,
					Transform = fieldAttr.Transform,
					SkipBuiltins = fieldAttr.SkipBuiltins,
					IsTextField = fieldAttr.IsTextField,
				    IsAggregation = fieldAttr.IsAggregation,
					Alias = prop.Name,
				    IsIdentifierOnly = isIdentifierOnly
				};
			}

			return new DbField
			{
				Field = prop.Name,
				Type = prop.PropertyType,
				GraphType = fieldAttr.ReturnType,
				ReverseTransform = fieldAttr.ReverseTransform,
				SkipBuiltins = fieldAttr.SkipBuiltins,
				IsTextField = fieldAttr.IsTextField,
			    IsAggregation = fieldAttr.IsAggregation,
				Transform = fieldAttr.Transform,
			    IsIdentifierOnly = isIdentifierOnly
			};
		}

		public BatchItem GetBatch(
			IDictionary<string, Field> ipSubFields,
			IDictionary<string, object> arguments,
			ResolveFieldContext context,
			PropertyInfo srcProperty = null
		)
		{
			var thisType = this.GetType();
			var sqlAttr = thisType.GetCustomAttribute<SqlGraphQLObjectAttribute>();
			var subFields = ipSubFields
				.Select(
					v =>
					{
						var property = thisType.GetProperty(v.Value.Name, SqlFieldResolver<T>.BindingFlags);
						var propertyAttr = (
							property?.GetCustomAttribute<SqlGraphQLFieldAttribute>()
							?? (object) property?.GetCustomAttribute<SqlFieldAttribute>()
							?? property?.GetCustomAttribute<SqlGraphQLRelatedAttribute>()
						);

						if (propertyAttr != null)
						{
							return new
							{
								field = v,
								property
							};
						}

						return null;
					})
				.Where(v => v != null)
				.Concat(
					ipSubFields
						.Select(
							v =>
							{
								var method = thisType.GetMethod(v.Value.Name, SqlFieldResolver<T>.BindingFlags);
								var methodAttr = method?.GetCustomAttribute<SqlGraphQLFuncAttribute>();


								return methodAttr?.RequiredProperties.Select(
									w => new
									{
										field = v,
										property = thisType.GetProperty(w, SqlFieldResolver<T>.BindingFlags)
									});
							})
						.Where(v => v != null)
						.SelectMany(v => v)
				)
				.ToList();

			var primaryProp = thisType.GetProperty(this.PrimaryProperty, SqlFieldResolver<T>.BindingFlags);
			if (primaryProp == null)
			{
				throw new ArgumentException("Invalid primary property set!");
			}

			var fields = subFields
				.Where(v => v.property != null)
				.Select(v => this.GetDbFieldFromProperty(v.property))
				.Append(this.GetDbFieldFromProperty(primaryProp, true))
				.Where(v => v != null)
				.Distinct();

			var whereArgs = arguments
				.Where(v => v.Key[0] != '_')
				.Select(
					v => new
					{
						key = v.Key,
						value = v.Value,
						property = thisType.GetProperty(v.Key, SqlFieldResolver<T>.BindingFlags)
					})
				.Select(
					v => new WhereArg
					{
						// ReSharper disable once PossibleInvalidOperationException
						Field = (DbField) this.GetDbFieldFromProperty(v.property),
						Key = v.key,
						Value = this.GetArgument<object>(v.value, context)
					})
				.Where((v) => v.Value != null);

			var batchItem = new BatchItem
			{
				Table = this.Table,
				Fields = fields.Cast<DbField>().ToList(),
				Where = whereArgs,
				PrimaryProperty = this.PrimaryProperty,
				DestType = thisType,
				SrcProperty = srcProperty,
				ExtraCriteriaField = sqlAttr?.ExtraCriteriaField,
				ExtraCriteriaValue = sqlAttr?.ExtraCriteriaValue,
				ChildQueries = subFields
					.Where(v => v.property != null)
					.Select(
						v => new
						{
							v.field,
							v.property,
							attr = v.property.GetCustomAttribute<SqlGraphQLRelatedAttribute>()
						})
					.Where(v => v.attr != null)
					.Select(
						v =>
						{
							var dstType = this.GetSqlFieldType(v.property.PropertyType);
							var resolver = SqlFieldResolverData.Resolvers[dstType];
							var result = resolver.GetBatch(
								v.field.Value.SelectionSet.Selections.Cast<Field>().ToDictionary(
									w => w.Name,
									w => w
								),
								v.field.Value.Arguments.ToDictionary(
									w => w.Name,
									w => w.Value.Value
								),
								context,
								v.property
							);

							result.LocalProperty = thisType.GetProperty(v.attr.LocalProperty);
							result.LocalField = this.GetDbFieldFromProperty(result.LocalProperty, true)?.Field;
							result.LocalJoinField = v.attr.LocalJoinField;
							result.ForeignProperty = dstType.GetProperty(v.attr.ForeignProperty);
							result.ForeignJoinField = v.attr.ForeignJoinField;
							result.ForeignField = this.GetDbFieldFromProperty(result.ForeignProperty, true)?.Field;
							result.JoinTable = v.attr.JoinTable;
							return result;
						})
			};

			var count = this.GetArgument<int?>("_fetchCount", arguments, context);
			var offset = this.GetArgument<int?>("_offset", arguments, context);
			var orderBy = this.GetArgument<IEnumerable<object>>("_orderBy", arguments, context)?.ToList();

			if (orderBy != null && orderBy.Count > 0)
			{
				batchItem.OrderBy = orderBy.Cast<IDictionary<string, object>>()
					.Select(
						v => new OrderByParameter
						{
							Field = (string) v["field"],
							Descending = (bool) v["descending"]
						})
					.Select(
						// ReSharper disable once ImplicitlyCapturedClosure
						v =>
						{
							var prop = thisType.GetProperty(v.Field, SqlFieldResolver<T>.BindingFlags);
							var newValue = prop == null ? null : this.GetDbFieldFromProperty(prop)?.Field;
							v.Field = newValue ?? v.Field;
							return v;
						});
			}
			else
			{
				batchItem.OrderBy = new List<OrderByParameter>
				{
					new OrderByParameter
					{
						Field = this.DefaultOrder,
						Descending = false
					}
				};
			}

			if (offset != null)
			{
				batchItem.Offset = offset;
			}

			if (count != null)
			{
				batchItem.Count = count;
			}

			return batchItem;
		}

		private TArgumentType GetArgument<TArgumentType>(string argumentName, IDictionary<string,object> arguments, ResolveFieldContext context)
		{
			return this.GetArgument<TArgumentType>(arguments.FirstOrDefault(v => v.Key == argumentName).Value, context);
		}

		private TArgumentType GetArgument<TArgumentType>(object value, ResolveFieldContext context)
		{
			if (value is string s)
			{
				var variable = context.Variables.FirstOrDefault(w => w.Name == s);
				if (variable != null)
				{
					value = variable.Value;
				}
			}

			return (TArgumentType) value;
		}

		private List<int> _lastChar = new List<int>();
		private readonly char[] _firstOpts = "_abcdefghijklmnopqrstuvwxyz".ToCharArray();
		private char[] OtherOpts => this._firstOpts.Concat("@#0123456789".ToCharArray()).ToArray();

		private void ResetAlias()
		{
			this._lastChar = new List<int>();
		}

		private void IncrementChar(int idx, int carryAt)
		{
			if (this._lastChar.Count <= idx)
			{
				this._lastChar.Add(0);
			}
			else
			{
				this._lastChar[idx]++;

				if (this._lastChar[idx] >= carryAt)
				{
					this._lastChar[idx] = 0;
					this.IncrementChar(idx + 1, this.OtherOpts.Length);
				}
			}
		}

	    public string GetNextAlias()
		{
			this.IncrementChar(0, this._firstOpts.Length);

			return String.Join(
				"",
				this._lastChar.Select(
					(v, i) =>
					{
						if (i == 0)
						{
							return this._firstOpts[v];
						}
						else
						{
							return this.OtherOpts[v];
						}
					}));
		}

		private BatchItem SetAliases(BatchItem batch)
		{
			batch.Alias = this.GetNextAlias();
			batch.Fields = batch.Fields.Select(
				v =>
				{
					v.Alias = batch.Alias + '_' + (v.Alias ?? v.Field);
					return v;
				}).ToList();
			batch.ChildQueries = batch.ChildQueries.Select(this.SetAliases).ToList();
			if (batch.ChildQueries.GroupBy(v => v.SrcProperty ?? v.LocalProperty).Any(v => v.Count() > 1))
			{
				throw new ArgumentException("You cannot query the same property more than once");
			}

			return batch;
		}

		public virtual IEnumerable<WhereItem> GetWhere(
			BatchItem batch,
			bool isSubquery,
			ResolveFieldContext context,
			bool isFirst)
		{
			var alias = isSubquery ? "" : $"[{batch.Alias}].";

			var result = batch.Where.Select(
				v =>
				{
					var value = $"@{batch.Alias}_{v.Key}";

					if (v.Field.ReverseTransform != null)
					{
						value = String.Format(v.Field.ReverseTransform, value);
					}

					var type = v.Field.GraphType ?? v.Field.Type;

					if (SqlFieldResolverData.TypeMap.ContainsKey(type))
					{
						return String.Format(
							SqlFieldResolverData.Parameters[SqlFieldResolverData.TypeMap[type]]
							.GetTemplate((IDictionary<string, object>) v.Value),
							$"{alias}[{v.Field.Field.Replace("]", "")}]",
							value
						);
					}
					else
					{
						return (WhereItem)$"{alias}[{v.Field.Field.Replace("]", "")}] = {value}";
					}
				});

			if (batch.ExtraCriteriaField != null)
			{
				result = result.Append(
					$"{alias}[{batch.ExtraCriteriaField.Replace("]", "")}] = @{batch.Alias}_Extra");
			}

			return result;
		}

		protected virtual Dictionary<string, DbValue> GetParameters(
			BatchItem batch,
			ResolveFieldContext context,
			Dictionary<string, DbValue> parameters = null)
		{
			if (parameters == null)
			{
				parameters = new Dictionary<string, DbValue>();
			}

			foreach (var v in batch.Where)
			{
				if (SqlFieldResolverData.TypeMap.ContainsKey(v.Field.Type))
				{
					object value;
					var field = SqlFieldResolverData.Parameters[SqlFieldResolverData.TypeMap[v.Field.Type]];
					if (v.Value is IDictionary<string, object> objects)
					{
						value = field.GetValue(objects);
					}
					else
					{
						value = v.Value;
					}

					if (value != null && value.GetType().IsArray)
					{
						var arrayValue = (object[]) value;

						for (var i = 0; i < arrayValue.Length; i++)
						{
							parameters.Add(
								$"@{batch.Alias}_{v.Key}_{i}",
								new DbValue(arrayValue[i])
							);
						}
					}
					else
					{
						parameters.Add(
							$"@{batch.Alias}_{v.Key}",
							new DbValue(value)
						);
					}
				}
				else
				{
					parameters.Add($"@{batch.Alias}_{v.Key}", new DbValue(v.Value));
				}
			}

			if (batch.ExtraCriteriaField != null)
			{
				parameters.Add($"@{batch.Alias}_Extra", new DbValue(batch.ExtraCriteriaValue));
			}

			foreach (var child in batch.ChildQueries)
			{
				this.GetParameters(child, context, parameters);
			}


		    var agg = context.GetService<AggregationSqlFieldGenerator>();
		    if (agg.IsAggregation(batch))
		    {
		        agg.GetExtraParams(batch, context, this, parameters);
		    }

			return parameters;
		}

		public IEnumerable<object> BuildObjects(
		    List<IEnumerable<IDictionary<string, object>>> resultSet,
		    BatchItem batch,
		    string joinTo = null,
		    object parent = null,
		    Func<IDictionary<string, object>, object> parentValueGetter = null,
		    object parentValue = null,
		    bool isAggMode = false,
		    Dictionary<string, Dictionary<NullObject<object>, IEnumerable<IDictionary<string, object>>>> cache = null
        )
		{
			if (!resultSet.Any())
			{
				return new List<object>();
			}

		    isAggMode = isAggMode || batch.Fields.Any(v => v.IsAggregation);

			var rows = this.GetRows(resultSet, $"{batch.Alias}_");

		    if (rows == null)
		    {
		        // This is caused by the aggregation, just pass through to the next level(s) with an empty object
		        var result = Activator.CreateInstance(batch.DestType);
		        result = this.HandleChildren(result, batch, resultSet, parentValueGetter, parentValue, isAggMode, cache);
		        return new [] { result };
		    }

			rows = this.FilterRows(rows.ToList(), isAggMode, parentValueGetter, parentValue, batch, joinTo, parent, cache);

			var key = $"{batch.Alias}_{batch.PrimaryProperty}";
		    Func<IDictionary<string, object>, object> valueGetter = v =>
		    {
			    return new NullObject<object>(v.ContainsKey(key) ? v[key] : null);
		    };
		    if (isAggMode)
		    {
		        // When aggregating we will be getting a single flat structure, just use the ROW_NUM
		        valueGetter = v => new NullObject<object>(v["ROW_NUM"]);
		    }

		    var childCache = new Dictionary<string, Dictionary<NullObject<object>, IEnumerable<IDictionary<string, object>>>>();

			return rows
				.GroupBy(valueGetter)
				.Where(v => v.Key != null)
				.Select(
					v =>
					{
						var result = this.CreateObject(batch, v);

					    return this.HandleChildren(result, batch, resultSet, valueGetter, v.Key, isAggMode, childCache);
					});
		}

		private object CreateObject(BatchItem batch, IGrouping<object, IDictionary<string, object>> rows)
		{
			var result = Activator.CreateInstance(batch.DestType);
			var selfProps = rows.First().Where(w => w.Key.StartsWith($"{batch.Alias}_"));
			var prefixLength = batch.Alias.Length + 1;

			foreach (var row in selfProps)
			{
				var value = row.Value;
				var prop = batch.DestType.GetProperty(row.Key.Substring(prefixLength));
				try
				{
					if (prop?.PropertyType?.IsNullable() == true && prop.PropertyType.GenericTypeArguments[0].IsEnum)
					{
						if (value == null)
						{
							prop.SetValue(result, null);
						}
						else
						{
							prop.SetValue(result, Enum.ToObject(prop.PropertyType.GenericTypeArguments[0], value));
						}
					}
					else
					{
						prop?.SetValue(result, value);
					}
				}
				catch (Exception e)
				{
					try
					{
						throw (Exception) e.GetType().GetConstructor(
							new[]
							{
								typeof(string),
								typeof(Exception)
							}
						)?.Invoke(
							new object[]
							{
								e.Message + $" while assigning {prop?.DeclaringType?.FullName}::{prop?.Name}",
								e
							}) ?? new Exception(e.Message + $" while assigning {prop?.DeclaringType?.FullName}::{prop?.Name}");
					}
					catch (Exception)
					{
						throw e;
					}
				}
			}

			return result;
		}

		private IEnumerable<IDictionary<string, object>> FilterRows(
			IReadOnlyCollection<IDictionary<string, object>> rows,
			bool isAggMode,
			Func<IDictionary<string, object>, object> parentValueGetter,
			object parentValue,
			BatchItem batch,
			string joinTo,
			object parent,
			IDictionary<string, Dictionary<NullObject<object>, IEnumerable<IDictionary<string, object>>>> cache
		)
		{
			if (isAggMode)
            {
                // No FKs in agg mode
                if (parentValueGetter != null && rows.Any(v => parentValueGetter(v) != null))
                {
                    return rows.Where(v => parentValueGetter(v).Equals(parentValue)).ToList();
                }
            }
            else
            {
                if (batch.ForeignField != null && joinTo != null && parent != null && cache != null)
                {
                    var fkId = $"{joinTo}_{batch.LocalProperty.Name.Replace("]", "")}";
                    var fkValue = batch.LocalProperty.GetValue(parent);
                    if (rows.First().Keys.Contains($"FK_{fkId}"))
                    {
                        fkId = $"FK_{fkId}";
                    }

	                var key = fkId + rows.GetHashCode();
                    if (!cache.ContainsKey(key))
                    {
                        cache[key] = rows.GroupBy(v => v[fkId]).ToDictionary(
                            v => new NullObject<object>(v.Key),
                            v => v.ToList().AsEnumerable()
                        );
                    }

	                if (cache[key].ContainsKey(fkValue))
	                {
		                return cache[key][fkValue];
	                }
	                else
	                {
		                return new List<IDictionary<string, object>>();
	                }
                }
            }

			return rows;
		}

		private IEnumerable<IDictionary<string, object>> GetRows(
			List<IEnumerable<IDictionary<string, object>>> resultSet,
			string prefix
		)
		{
			return resultSet.FirstOrDefault(
				table => table.First().Keys.FirstOrDefault(w => w.StartsWith(prefix)) != null
			)?.ToList();
		}

		private object HandleChildren(object result,
	        BatchItem batch,
	        List<IEnumerable<IDictionary<string, object>>> resultSet,
	        Func<IDictionary<string, object>, object> valueGetter,
	        object parentValue,
	        bool isAggMode,
	        Dictionary<string, Dictionary<NullObject<object>, IEnumerable<IDictionary<string, object>>>> cache)
	    {
	        foreach (var child in batch.ChildQueries)
	        {
	            var childResult = this.BuildObjects(resultSet, child, batch.Alias, result, valueGetter, parentValue, isAggMode, cache);
	            try
	            {
	                var srcType = child.SrcProperty.PropertyType;
	                if (
	                    srcType.IsGenericType
	                    && srcType == typeof(IEnumerable<>).MakeGenericType(srcType.GenericTypeArguments[0])
	                )
	                {
	                    child.SrcProperty.SetValue(
	                        result,
	                        typeof(Enumerable).GetMethod("Cast")?
	                            .MakeGenericMethod(srcType.GenericTypeArguments[0])
	                            .Invoke(null, new object[] {childResult.ToList()})
	                    );
	                }
	                else
	                {
	                    child.SrcProperty.SetValue(
	                        result,
	                        childResult.FirstOrDefault()
	                    );
	                }
	            }
	            catch (Exception e)
	            {
	                try
	                {
	                    throw (Exception) e.GetType().GetConstructor(
	                        new[]
	                        {
	                            typeof(string),
	                            typeof(Exception)
	                        }
	                    )?.Invoke(
	                        new object[]
	                        {
	                            e.Message + $" while assigning {child.SrcProperty?.DeclaringType?.FullName}::{child.SrcProperty?.Name}",
	                            e
	                        }) ?? new Exception(
								e.Message + $" while assigning {child.SrcProperty?.DeclaringType?.FullName}::{child.SrcProperty?.Name}",
								e
		                    );
	                }
	                catch (Exception)
	                {
	                    // ignored
	                }

	                throw;
	            }
	        }

	        return result;
	    }

	    public string GetFieldSql(string batchAlias, DbField field)
	    {
	        var transform = field.Transform;

	        if (String.IsNullOrEmpty(transform) && field.Type == typeof(string) && !field.SkipBuiltins)
	        {
	            transform = "IsNull([{0}].[{1}], " + (field.IsTextField ? "CAST('' AS TEXT)" : "''") + ")";
	        }

	        if (transform != null)
	        {
	            return String.Format(transform, batchAlias, field.Field.Replace("]", ""));
	        }

	        return (
	            $"[{batchAlias}].[{field.Field.Replace("]", "")}]"
	        );
	    }

	    public virtual IEnumerable<string> GetSelect(
		    BatchItem batch,
		    string joinTo,
		    bool includeIds = true,
		    bool isFirst = true,
		    IEnumerable<DbField> fields = null,
		    bool includeAll = false
		)
	    {
		    if(batch.DestType != this.GetType())
		    {
			    return SqlFieldResolverData.Resolvers[batch.DestType].GetSelect(
				    batch,
				    joinTo,
				    includeIds,
				    isFirst,
				    fields,
				    includeAll
			    );
		    }

	        var result = (fields ?? batch.Fields)
	            .Select(v => $"{this.GetFieldSql(batch.Alias, v)} AS [{v.Alias.Replace("]", "")}]");
	        if (batch.ChildQueries.Count() == 1 || includeAll)
	        {
	            result = result.Concat(
		            batch.ChildQueries.SelectMany((child) => this.GetSelect(child, null, includeIds, false))
		        );
	            if (includeIds)
	            {
	                result = result.Concat(
		                batch.ChildQueries.Select((child) => $"[{batch.Alias}].[{child.LocalField.Replace("]", "")}] AS [{batch.Alias}_{child.LocalProperty.Name.Replace("]", "")}]")
			        );
	            }
	        }

	        if (joinTo != null)
	        {
	            result = result.Append(
	                $"[{joinTo}].[{batch.LocalField.Replace("]", "")}] AS [FK_{joinTo}_{batch.LocalProperty.Name.Replace("]", "")}]");
	        }

	        return result;
	    }

		public string BuildQuery(
			BatchItem batch,
			ResolveFieldContext context,
			string previousFrom = null,
			string joinTo = null)
		{
		    var agg = context.GetService<AggregationSqlFieldGenerator>();
		    if (agg.IsAggregation(batch))
		    {
		        return agg.BuildQuery(batch, context, this, previousFrom, joinTo);
		    }

			return context.GetService<StandardSqlFieldGenerator>().BuildQuery(batch, context, this, previousFrom, joinTo);
		}

		public virtual int GetCount(IDictionary<string, object> arguments, ResolveFieldContext context)
		{
			var parent = CountResolver.GetParentNode(context.Document, context.FieldAst);
			var siblings = parent.Children.Cast<Field>().Where(v => v.Name != context.FieldAst.Name).ToList();
			if (siblings.Count == 0)
			{
				throw new CountResolverException("No sibling found");
			}
			else if (siblings.Count > 1)
			{
				throw new CountResolverException("Multiple siblings found, _count only works with one sibling");
			}

			var sibling = siblings.First();
			var siblingArguments = sibling.Arguments
				.Where(v => v.Name != "_fetchCount")
				.Where(v => v.Name != "_offset")
				.ToDictionary(
					(v) => v.Name,
					(v) => v.Value.Value
				);

			var baseBatch = this.GetBatch(
				sibling.SelectionSet.Children
					.Where((v) => v is Field)
					.Cast<Field>()
					.ToDictionary(
						(v) => v.Name,
						(v) => v
					),
				siblingArguments,
				context
			);

			this.ResetAlias();

			string query;

			Dictionary<string, DbValue> parameters;
			var agg = context.GetService<AggregationSqlFieldGenerator>();
			if (agg.IsAggregation(baseBatch))
			{
				var oldArguments = context.Arguments;
				context.Arguments = siblingArguments;
				baseBatch = this.SetAliases(baseBatch);
				query = this.BuildQuery(baseBatch, context);
				parameters = this.GetParameters(baseBatch, context);
				context.Arguments = oldArguments;

				query = new Regex("SELECT \\* FROM").Replace(query, "SELECT COUNT(*) FROM");
				query = new Regex("ORDER BY [^ ]*$").Replace(query, "");
			}
			else
			{
				var batch = this.GetBatch(new Dictionary<string, Field>(), arguments, context);


				batch.ChildQueries = new List<BatchItem>();
				batch.Fields = new List<DbField>
				{
					new DbField
					{
						RawField = "count(*) AS [count]",
						Field = "count"
					}
				};

				batch.OrderBy = null;
				batch = this.SetAliases(batch);

				query = this.BuildQuery(batch, context);
				parameters = this.GetParameters(batch, context);
			}

		    var results = new List<IEnumerable<IDictionary<string, object>>>();

		    using (var connection = this.GetConnection(context))
		    {
		        using (var command = new SqlCommand(query, connection))
		        {
		            foreach (var param in parameters)
                    {
                        var name = param.Key;
                        var value = param.Value;
		                command.Parameters.Add(name, value.Type);
		                command.Parameters[name].Value = value.Value ?? DBNull.Value;
		            }

		            connection.Open();

		            using (var reader = command.ExecuteReader())
		            {
		                do
		                {
		                    var list = new List<IDictionary<string, object>>();
		                    while (reader.Read())
		                    {
		                        var res = new Dictionary<string, object>();
		                        for (var i = 0; i < reader.FieldCount; i++)
		                        {
		                            if (reader.IsDBNull(i))
		                            {
		                                res[reader.GetName(i)] = null;
		                            }
		                            else
		                            {
		                                res[reader.GetName(i)] = reader.GetValue(i);
		                            }
		                        }

		                        list.Add(res);
		                    }

		                    results.Add(list);
		                } while (reader.NextResult());
		            }
		        }
		    }

			return (int) results.First().First().First().Value;
		}

		private SqlConnection GetConnection(ResolveFieldContext context)
		{
			return context.GetService<ISqlConnectionGetter>().GetConnection(context);
		}

		public virtual object Resolve(ResolveFieldContext context)
		{
			Dictionary<string, object> baseMetricMetadata = null;
			try
			{
				baseMetricMetadata =
					context.Metrics.AllRecords.LastOrDefault((v) => v.Category == "field")
						?.Metadata;
			} catch(ArgumentNullException) {}

			using (this.Subject(context, baseMetricMetadata))
		    {
			    var batch = this.GetBatch(context.SubFields, context.Arguments, context);
			    this.ResetAlias();
			    batch = this.SetAliases(batch);

			    string query;
			    Dictionary<string, DbValue> parameters;

			    using (this.Subject("queryBuild", context, baseMetricMetadata))
			    {
				    query =
					    new Regex("[ \\t\\r\\n]+", RegexOptions.Multiline).Replace(
						    this.BuildQuery(batch, context),
						    " ");
				    parameters = this.GetParameters(batch, context);
			    }

			    try
			    {
				    var results = new List<IEnumerable<IDictionary<string, object>>>();

				    using (this.Subject(
					    "queryExecute",
					    context,
					    baseMetricMetadata != null ? new Dictionary<string, object> (baseMetricMetadata)
					    {
						    {
							    "parameters",
							    String.Join("\n", parameters
										.Select(
											pair =>
											{
												var value = pair.Value.Value;
												if (value is string)
												{
													value = $"'{value}'";
												}

												var type = pair.Value.Type.ToString();
												if (pair.Value.Type == SqlDbType.NVarChar)
												{
													type += "(MAX)";
												}

												return $"DECLARE {pair.Key} {type} = {value};";
											})
								)
						    },
						    {"query", query}
					    } : null))
				    {
					    using (var connection = this.GetConnection(context))
					    {
						    using (var command = new SqlCommand(query, connection))
						    {
							    foreach (var param in parameters)
							    {
								    var name = param.Key;
								    var value = param.Value;
								    command.Parameters.Add(name, value.Type);
								    command.Parameters[name].Value = value.Value ?? DBNull.Value;
							    }

							    connection.Open();

							    using (var reader = command.ExecuteReader())
							    {
								    do
								    {
									    var list = new List<IDictionary<string, object>>();
									    while (reader.Read())
									    {
										    var res = new Dictionary<string, object>();
										    for (var i = 0; i < reader.FieldCount; i++)
										    {
											    if (reader.IsDBNull(i))
											    {
												    res[reader.GetName(i)] = null;
											    }
											    else
											    {
												    res[reader.GetName(i)] = reader.GetValue(i);
											    }
										    }

										    list.Add(res);
									    }

									    results.Add(list);
								    } while (reader.NextResult());
							    }
						    }
					    }
				    }

				    using (this.Subject(
					    "buildObjects",
					    context,
					    baseMetricMetadata != null ? new Dictionary<string, object> (baseMetricMetadata)
					    {
						    { "ResultSets", (int?)results.Count },
						    { "ResultSetCounts", results.Select(v => v.Count()) }
					    } : null)
				    )
				    {
					    return this.BuildObjects(
						    results.Where(v => v.Any()).ToList(),
						    batch
					    ).ToList();
				    }
			    }
			    catch (SqlException e)
			    {
				    throw new Exception(
					    $"Failed to execute sql {query} using arguments {JsonConvert.SerializeObject(parameters)} => {e}");
			    }
		    }
		}

		private IDisposable Subject(ResolveFieldContext context, Dictionary<string, object> baseMetricMetadata)
		{
			if (baseMetricMetadata == null)
			{
				return null;
			}

			var metadata = new Dictionary<string, object>(baseMetricMetadata);
			var path = (string[]) baseMetricMetadata["path"];
			metadata["path"] = path.Concat(new[] {"sql"});

			return context.Metrics.Subject(
				"field",
				"sql",
				metadata
			);
		}

		private IDisposable Subject(string subject, ResolveFieldContext context, IDictionary<string, object> baseMetricMetadata)
		{
			if (baseMetricMetadata == null)
			{
				return null;
			}

			var metadata = new Dictionary<string, object>(baseMetricMetadata);
			var path = (string[]) baseMetricMetadata["path"];
			metadata["path"] = path.Concat(new[] {"sql", subject});
			metadata["sqlTrace"] = true;

			return context.Metrics.Subject(
				"field",
				subject,
				metadata
			);
		}

		public static IEnumerable<QueryArgument> GetArgumentsForType(bool isRoot)
		{
			var result = typeof(T)
				.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance)
				.Select(
					prop => new
					{
						prop,
						fieldAttr = prop.GetCustomAttribute<SqlGraphQLFieldAttribute>()
					})
				.Where(v => v.fieldAttr != null)
				.Select(
					v => new QueryArgument(
						SqlFieldResolver<T>.GetParamType(v.fieldAttr.ReturnType ?? v.prop.PropertyType.ToGraphType()))
					{
						Name = v.fieldAttr.Name ?? Utils.FirstCharacterToLower(v.prop.Name),
						Description = v.fieldAttr.Description
					})
			    .Prepend(
			        new QueryArgument<ListGraphType<StringGraphType>>
			        {
			            Name = "_groupBy",
			            Description = "By default columns will not be grouped unless _count is requested, then all " +
			                          "columns will be grouped by. If you supply this parameter the defaults will be " +
			                          "turned off and the records will first be grouped by these fields, then will be " +
			                          "distincted when joined the rest of the fields. This field should contain an array " +
			                          "of strings containing the object.field notation of the desired grouping"
			        });
			if (isRoot)
			{
				result = result
					.Prepend(
						new QueryArgument<ListGraphType<OrderByParameter>>
							{Name = "_orderBy"})
					.Prepend(
						new QueryArgument<IntGraphType>
							{Name = "_offset"})
				    .Prepend(
				        new QueryArgument<IntGraphType>
				            {Name = "_fetchCount"});
			}

			return result;
		}

		private static Type GetParamType(Type type)
		{
			if (SqlFieldResolverData.TypeMap.ContainsKey(type))
			{
				return SqlFieldResolverData.TypeMap[type].ToGraphType();
			}

			return type;
		}
	}

	public struct DbValue
    {
        private static Dictionary<Type, SqlDbType> typeMap = new Dictionary<Type, SqlDbType>
        {
            {typeof(string), SqlDbType.NVarChar},
            {typeof(char[]), SqlDbType.NVarChar},
            {typeof(byte), SqlDbType.TinyInt},
            {typeof(short), SqlDbType.SmallInt},
            {typeof(int), SqlDbType.Int},
            {typeof(long), SqlDbType.BigInt},
            {typeof(byte[]), SqlDbType.Image},
            {typeof(bool), SqlDbType.Bit},
            {typeof(DateTime), SqlDbType.DateTime2},
            {typeof(DateTimeOffset), SqlDbType.DateTimeOffset},
            {typeof(decimal), SqlDbType.Money},
            {typeof(float), SqlDbType.Real},
            {typeof(double), SqlDbType.Float},
            {typeof(TimeSpan), SqlDbType.Time}
        };

        public SqlDbType Type;
        public object Value;

        public DbValue(object value)
        {
            this.Value = value;
            this.Type = value == null ? SqlDbType.NVarChar : DbValue.typeMap[value.GetType()];
        }

        public DbValue(object value, SqlDbType type)
        {
            this.Value = value;
            this.Type = type;
        }
    }
}
