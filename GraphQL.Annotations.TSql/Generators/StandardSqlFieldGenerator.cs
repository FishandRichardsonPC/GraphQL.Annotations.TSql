using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Generators
{
    internal class StandardSqlFieldGenerator
    {
		private IEnumerable<string> GetTables<T>(
		    BatchItem batch,
		    string joinTo,
		    ResolveFieldContext context,
		    SqlFieldResolver<T> resolver
		) where T : SqlFieldResolver<T>, new()
		{
			var table = $"[{batch.Table.Replace("]", "")}] AS [{batch.Alias}] WITH (NOLOCK)";

			var where = resolver.GetWhere(batch, false, context, joinTo == null).ToList();

			if (batch.RequiresSubquery || (joinTo == null && where.Count > 0))
			{
				where = resolver.GetWhere(batch, true, context, joinTo == null).ToList();
				table = $"(SELECT * FROM [{batch.Table.Replace("]", "")}] AS [{batch.Alias}] WITH (NOLOCK)";

				if (where.Count > 0)
				{
					table += $" WHERE {String.Join(" AND ", where)}";
				}

				if (batch.OrderBy != null)
				{
					table +=
						" ORDER BY "
						+ String.Join(
							",",
							batch.OrderBy
							    .Where(v => v.Field != "")
							    .Select(v => $"[{v.Field.Replace("]", "")}] {(v.Descending ? "DESC" : "ASC")}")
						)
						+ $" OFFSET {batch.Offset ?? 0} ROWS";

					if (batch.Count != null)
					{
						table += $" FETCH NEXT {batch.Count} ROWS ONLY";
					}
				}

				table += $") AS [{batch.Alias}]";
			}

			if (batch.JoinTable == null)
			{
				if (joinTo != null)
				{
					table += String.Join(
						" AND ",
						where.Prepend(
							$" ON [{joinTo}].[{batch.LocalField.Replace("]", "")}] = [{batch.Alias}].[{batch.ForeignField.Replace("]", "")}]"));
				}
			}
			else
			{
				if (joinTo != null)
				{
					var joinAlias = resolver.GetNextAlias();
					table = String.Join(
						" AND ",
						where.Prepend(
							$"[{batch.JoinTable.Replace("]", "")}] AS [{joinAlias}] ON [{joinTo}].[{batch.LocalField.Replace("]", "")}] = [{joinAlias}].[{batch.LocalJoinField.Replace("]", "")}] "
							+ $"LEFT JOIN {table} ON [{joinAlias}].[{batch.ForeignJoinField.Replace("]", "")}] = [{batch.Alias}].[{batch.ForeignField.Replace("]", "")}]"
						));
				}
			}

			var result = (IEnumerable<string>) new List<string> {table};
			if (batch.ChildQueries.Count() == 1)
			{
				result = result.Concat(this.GetTables(batch.ChildQueries.First(), batch.Alias, context, resolver));
			}

			return result;
		}

        public string BuildQuery<T>(BatchItem batch,
            ResolveFieldContext context,
            SqlFieldResolver<T> resolver,
            string previousFrom = null,
            string joinTo = null
        ) where T : SqlFieldResolver<T>, new()
        {
            var from = String.Join(
                " LEFT JOIN ",
                this.GetTables(batch, joinTo, context, resolver).Prepend(previousFrom).Where(v => v != null));
            var query =
                $"SELECT {String.Join(",", resolver.GetSelect(batch, joinTo))} " + $"FROM {from}";

            var nextBatches = batch.ChildQueries.ToList();

            while (nextBatches.Count == 1)
            {
	            batch = nextBatches[0];
                nextBatches = nextBatches[0].ChildQueries.ToList();
            }

            if (nextBatches.Count > 1)
            {
                query = String.Join(
                    "\n",
                    nextBatches.Select(b => this.BuildQuery(b, context, resolver, from, batch.Alias)).Prepend(query));
            }

            return query;
        }
    }
}