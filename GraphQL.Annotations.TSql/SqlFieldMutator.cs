using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Dapper;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql
{
	public static class SqlFieldMutator
	{
		private static BindingFlags _bindingFlags =
			BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

		public static T Mutate<T, TMutable>(IResolveFieldContext context, TMutable input)
			where TMutable : SqlFieldResolver<TMutable>, new()
			where T : SqlFieldResolver<T>, new()
		{
			var fields = ((IDictionary<string, object>) context.Arguments.First().Value)
				.Select(
					v =>
					{
						var prop = typeof(TMutable).GetProperty(
							v.Key,
							SqlFieldMutator._bindingFlags);
						var attr =
							(ISqlFieldAttribute) prop.GetCustomAttribute<SqlGraphQLFieldAttribute>()
							?? prop.GetCustomAttribute<SqlFieldAttribute>();
						return new
						{
							Property = prop,
							Attribute = attr,
							Name = attr?.DbFieldName?.Replace("]", "") ?? prop?.Name,
							Value = prop?.GetValue(input)
						};
					})
				.Where(v => (v.Property ?? (object) v.Attribute ?? v.Name) != null)
				.ToList();

			var ids = fields
				.Where(v => v.Attribute.ReturnType == typeof(IdGraphType))
				.ToList();
			var notIds = fields
				.Where(v => v.Attribute.ReturnType != typeof(IdGraphType))
				.ToList();
			var notIdentity = fields
				.Where(v => !v.Attribute.SkipOnInsert)
				.ToList();

			IDictionary<string, object> result;
			if (notIds.Any())
			{
				if (!ids.Any())
				{
					throw new ArgumentException(
						"You must supply one or more fields with ReturnType of IdGraphType for SQL mutations to work");
				}

				var query = (
					"DECLARE @temp_temp_temp TABLE ("
					+ String.Join(
						", ",
						ids.Select(v => $"{v.Name} {SqlFieldMutator.GetSqlType(v.Property)}")
					)
					+ $"); MERGE [{input.Table}] AS [target] USING (SELECT "
					+ String.Join(
						", ",
						fields.Select(v => $"@{v.Name} AS [{v.Name}]")
					)
					+ ") AS [source] ON "
					+ String.Join(
						" AND ",
						ids.Select(v => $"[target].[{v.Name}] = [source].[{v.Name}]")
					)
					+ " WHEN MATCHED THEN UPDATE SET "
					+ String.Join(
						", ",
						notIds.Select(v => $"[target].[{v.Name}] = [source].[{v.Name}]")
					)
					+ " WHEN NOT MATCHED THEN INSERT ("
					+ String.Join(
						", ",
						notIdentity.Select(v => $"[{v.Name}]")
					)
					+ ") VALUES ("
					+ String.Join(
						", ",
						notIdentity.Select(v => $"[source].[{v.Name}]")
					)
					+ ") OUTPUT "
					+ String.Join(
						", ",
						ids.Select(
							v =>
							{
								if (String.IsNullOrEmpty(v.Attribute.Transform))
								{
									return $"[INSERTED].[{v.Name}]";
								}
								else
								{
									return String.Format(v.Attribute.Transform, "INSERTED", v.Name);
								}
							})
					)
					+ " INTO @temp_temp_temp; SELECT * FROM @temp_temp_temp"
				);

				var queryParams = new DynamicParameters();

				foreach (var field in fields)
				{
					queryParams.Add($"@{field.Name}", field.Value);
				}

				using (var connection = context.GetRequiredService<ISqlConnectionGetter>()
					.GetConnection(context))
				{
					try
					{
						result = (IDictionary<string, object>) connection.QueryFirst(
							query,
							queryParams);
					}
					catch (SqlException e)
					{
						throw new Exception(
							$"Failed to execute sql {query} using arguments {JsonSerializer.Serialize(queryParams)} => {e}");
					}
				}
			}
			else
			{
				result = ids.ToDictionary(
					v => v.Name,
					v => v.Value
				);
			}

			var resolver = context.GetRequiredService<T>();
			context.Arguments.Clear();

			foreach (var (key, value) in result)
			{
				context.Arguments.Add(fields.First(w => w.Name == key).Property.Name, value);
			}

			return (T) ((List<object>) resolver.Resolve(context)).First();
		}

		private static string GetSqlType(PropertyInfo prop)
		{
			var attr = prop.GetCustomAttribute<SqlFieldAttribute>();
			if (attr?.SqlType != null)
			{
				return attr.SqlType;
			}

			if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
			{
				return "uniqueidentifier";
			}
			else
			{
				return "text";
			}
		}

		public static void Delete<TMutationType, TIdType>(
			IResolveFieldContext context,
			TIdType id,
			string idProperty = "Id"
		)
		{
			var prop = typeof(TMutationType).GetProperty(idProperty);
			var attr = prop?.GetCustomAttribute<SqlFieldAttribute>();
			var idField = attr?.DbFieldName?.Replace("]", "") ?? prop?.Name;

			using (var connection = context.GetRequiredService<ISqlConnectionGetter>()
				.GetConnection(context))
			{
				var resolver =
					(ISqlFieldResolver) context.GetRequiredService(typeof(TMutationType));
				var query = $"DELETE FROM {resolver.Table} WHERE {idField} = @id";
				var queryParams = new {id};
				try
				{
					connection.Execute(query, queryParams);
				}
				catch (SqlException e)
				{
					throw new Exception(
						$"Failed to execute sql {query} using arguments {JsonSerializer.Serialize(queryParams)} => {e}");
				}
			}
		}
	}
}
