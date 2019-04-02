using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Annotations.TSql.ParameterTypes;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Generators
{
    internal class SimpleAggregationSqlFieldGenerator
    {
        public IEnumerable<string> GetFields(BatchItem batch)
        {
            var result = batch.Fields
                .Where(v => !v.IsAggregation)
                .Where(v => !v.IsIdentifierOnly)
                .Select(
                    v =>
                    {
                        var transform = v.Transform;

                        if (String.IsNullOrEmpty(transform) && v.Type == typeof(string) && !v.SkipBuiltins)
                        {
                            transform = "IsNull([{0}].[{1}], " + (v.IsTextField ? "CAST('' AS TEXT)" : "''") + ")";
                        }

                        if (transform != null)
                        {
                            return String.Format(transform, batch.Alias, v.Field.Replace("]", ""));
                        }

                        return (
                            $"[{batch.Alias}].[{v.Field.Replace("]", "")}]"
                        );
                    });

            result = result
                .Concat(batch.ChildQueries.SelectMany(this.GetFields));

            return result;
        }

        public IEnumerable<string> GetHaving<T>(
            BatchItem batch,
            ResolveFieldContext context,
            SqlFieldResolver<T> resolver,
            bool isFirst = true
        ) where T: SqlFieldResolver<T>, new()
        {
            return this.GetHavingEnumerable(batch, context)
                .Select(v =>
                {
                    var value = $"@{v.Item1.Alias}";

                    var type = v.Item1.GraphType ?? v.Item1.Type;

                    if (SqlFieldResolverData.TypeMap.ContainsKey(type))
                    {
	                    var templateValue = v.Item2 as IDictionary<string, object>;

	                    if (templateValue == null && v.Item2 is string s)
	                    {
		                    templateValue = (IDictionary<string, object>)context.Variables.First(w => w.Name == s).Value;
	                    }

                        return templateValue == null ? null : String.Format(
	                        SqlFieldResolverData.Parameters[SqlFieldResolverData.TypeMap[type]]
                            .GetTemplate(templateValue),
                            v.Item1.Transform,
                            value
                        );
                    }
                    else
                    {
                        return $"[{v.Item1.Field.Replace("]", "")}] = {v.Item1.Transform}";
                    }
                })
                .Where((v) => !String.IsNullOrEmpty(v));
        }

        public IEnumerable<WhereItem> GetWhere<T>(
            BatchItem batch,
            ResolveFieldContext context,
            SqlFieldResolver<T> resolver,
            bool isFirst = true
        ) where T: SqlFieldResolver<T>, new()
        {
            return resolver
                .GetWhere(batch, true, context, isFirst)
                .Concat(batch.ChildQueries.SelectMany(v => this.GetWhere(v, context, resolver, false)));
        }

        public string BuildQuery<T>(
            BatchItem batch,
            ResolveFieldContext context,
            SqlFieldResolver<T> resolver,
            string previousFrom,
            string joinTo
        ) where T : SqlFieldResolver<T>, new()
        {
            batch = this.RemoveIdentifiers(batch);

            var from = String.Join(
                " JOIN ",
                this.GetTables(batch, joinTo, resolver).Prepend(previousFrom).Where(v => v != null));
            var select = resolver.GetSelect(batch, joinTo, false, true, null, true)
                .Select(v => v.Split(new [] {" AS "}, StringSplitOptions.None))
                .ToList();

            var filteredSelect = select
                .Where((v, i) => select.FindIndex(w => w.Last() == v.Last()) == i)
                .ToList();

            var query =
                $"{String.Join(",", filteredSelect.Select(v => String.Join(" AS ", v)))} FROM {from}";

	        var allWhere = this.GetWhere(batch, context, resolver).ToList();
	        var where = allWhere.Where((v) => !v.IsSecurity).ToList();

            if (where.Any())
            {
                query += $"  WHERE {String.Join(" AND ", where)}";
            }

            var orderByList = this.GetOrderBy(batch, resolver);

	        select = resolver.GetSelect(
			        batch,
			        joinTo,
			        false,
			        true,
			        batch.Fields.Where((v) => !v.IsAggregation),
			        true
			)
		        .Select(v => v.Split(new [] {" AS "}, StringSplitOptions.None))
		        .ToList();

	        filteredSelect = select
		        .Where((v, i) => select.FindIndex(w => w.Last() == v.Last()) == i)
		        .ToList();

            query += " GROUP BY " + String.Join(
                 ", ",
				filteredSelect.Select(v => v[0]).Concat(orderByList.Where((v) => !v.IsAggregation).Select((v) => v.Field)).Distinct()
            );

            var having = this.GetHaving(batch, context, resolver).ToList();

            if (having.Any())
            {
                query += $"  HAVING {String.Join(" AND ", having)}";
            }

            var orderBy = $"ORDER BY {String.Join(", ", orderByList.Select((v) => $"{v.Field} {v.Direction}"))}";

            var whereClauses = allWhere
	            .Where((v) => v.IsSecurity)
	            .Select((v) => v.ToString(""))
	            .ToList();

	        var cteName = "cte_" + Guid.NewGuid().ToString("N");
            query = (
                $"WITH {cteName} AS (" +
                    $"SELECT ROW_NUMBER() OVER({orderBy}) AS ROW_NUM," +
                    query +
                $") SELECT * FROM [{cteName}]"
            );

            if (whereClauses.Any())
            {
                query += $" WHERE {String.Join(" AND ", whereClauses)}";
            }

	        query += " ORDER BY ROW_NUM";

            // Rows are 1 indexed so # > 0 AND # <= Count
            if (batch.Offset != null || batch.Count != null)
            {
	            query += $" OFFSET {batch.Offset ?? 0} ROWS";

	            if (batch.Count != null)
	            {
		            query += $" FETCH NEXT {batch.Count} ROWS ONLY";
	            }
            }

	        query += ';';

            return query;
        }

        private BatchItem RemoveIdentifiers(BatchItem batch)
        {
            batch.Fields = batch.Fields.Where(v => !v.IsIdentifierOnly).ToList();
            batch.ChildQueries = batch.ChildQueries.Select(this.RemoveIdentifiers).ToList();
            return batch;
        }

        private List<OrderByItem> GetOrderBy<T>(
            BatchItem batch,
            SqlFieldResolver<T> resolver
        ) where T : SqlFieldResolver<T>, new()
        {
            var orderByList = batch.OrderBy
                .Select(v =>
                {
                    var field = batch.Fields.Cast<DbField?>().FirstOrDefault(w => w?.Field == v.Field);

                    if (field == null)
                    {
                        return null;
                    }

                    return new
                    {
                        OrderBy = v,
                        Field = (DbField)field
                    };
                })
                .Where((v) => v != null)
                .ToArray();

            if (!orderByList.Any())
            {
                var firstField = batch.Fields
                    .Where((v) => !v.IsAggregation)
                    .Cast<DbField?>()
                    .FirstOrDefault();
                if (firstField != null)
                {
                    orderByList = new[]
                    {
                        new
                        {
                            OrderBy = new OrderByParameter
                            {
                                Descending = true
                            },
                            Field = (DbField)firstField
                        }
                    };
                }
            }

            return orderByList
                .Select(v => new OrderByItem {
                    Field = resolver.GetFieldSql(batch.Alias, v.Field),
                    Direction = v.OrderBy.Descending ? "DESC" : "ASC",
                    IsAggregation = v.Field.IsAggregation
                })
                .Concat(batch.ChildQueries.SelectMany(v => this.GetOrderBy(v, resolver)))
                .Where(v => !String.IsNullOrEmpty(v.Field))
                .ToList();
        }

        private IEnumerable<string> GetTables<T>(
		    BatchItem batch,
		    string joinTo,
		    SqlFieldResolver<T> resolver
		) where T : SqlFieldResolver<T>, new()
		{
			var table = $"[{batch.Table.Replace("]", "")}] AS [{batch.Alias}] WITH (NOLOCK)";

			if (batch.JoinTable == null)
			{
				if (joinTo != null)
				{
					table += $" ON [{joinTo}].[{batch.LocalField.Replace("]", "")}] = [{batch.Alias}].[{batch.ForeignField.Replace("]", "")}]";
				}
			}
			else
			{
				if (joinTo != null)
				{
					var joinAlias = resolver.GetNextAlias();
					table = $"[{batch.JoinTable.Replace("]", "")}] AS [{joinAlias}] ON [{joinTo}].[{batch.LocalField.Replace("]", "")}] = [{joinAlias}].[{batch.LocalJoinField.Replace("]", "")}] "
                        + $"JOIN {table} ON [{joinAlias}].[{batch.ForeignJoinField.Replace("]", "")}] = [{batch.Alias}].[{batch.ForeignField.Replace("]", "")}]";
				}
			}

			var result = (IEnumerable<string>) new List<string> {table};
			result = result.Concat(batch.ChildQueries.SelectMany((child) => this.GetTables(child, batch.Alias, resolver)));

			return result;
		}

        public void GetExtraParams<T>(BatchItem batch, ResolveFieldContext context, SqlFieldResolver<T> resolver, Dictionary<string, DbValue> parameters) where T : SqlFieldResolver<T>, new()
        {
            foreach (var (dbField, paramValue) in this.GetHavingEnumerable(batch, context))
            {
                if (SqlFieldResolverData.TypeMap.ContainsKey(dbField.Type))
                {
                    object value;
	                var field =
		                SqlFieldResolverData.Parameters[SqlFieldResolverData.TypeMap[dbField.Type]];

	                var param = paramValue;
	                if (param is string s)
	                {
		                var variable = context.Variables.FirstOrDefault(w => w.Name == s);
		                if (variable != null)
		                {
			                param = variable.Value;
		                }
	                }

	                if (param is IDictionary<string, object> objects)
	                {
		                value = field.GetValue(objects);
	                }
	                else
	                {
		                value = param;
	                }

                    if (value != null && value.GetType().IsArray)
                    {
                        var arrayValue = (object[]) value;

                        for (var i = 0; i < arrayValue.Length; i++)
                        {
                            parameters.Add(
                                $"@{dbField.Alias}_{i}",
                                new DbValue(arrayValue[i])
                            );
                        }
                    }
                    else
                    {
                        parameters.Add(
                            $"@{dbField.Alias}",
                            new DbValue(value)
                        );
                    }
                }
                else
                {
                    parameters.Add($"@{dbField.Alias}", new DbValue(paramValue));
                }
            }
        }

        private IEnumerable<Tuple<DbField, object>> GetHavingEnumerable(BatchItem batch, ResolveFieldContext context)
        {
            return context.Arguments
                .Select(v => new
                {
                    // ReSharper disable once PossibleInvalidOperationException
                    Field = batch.Fields.Cast<DbField?>().FirstOrDefault(w => (bool) w?.Alias.EndsWith($"_{v.Key}")),
                    v.Value
                })
                .Where(v => v.Field != null)
                .Select(v => new Tuple<DbField, object>(
                    // ReSharper disable once PossibleInvalidOperationException
                    (DbField) v.Field,
                    v.Value
                ))
                .Where(v => v.Item1.IsAggregation);
        }
    }

    internal struct OrderByItem
    {
	    public string Field { get; set; }
	    public string Direction { get; set; }
	    public bool IsAggregation { get; set; }
    }
}