using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Generators
{
    internal class AggregationSqlFieldGenerator
    {
        private readonly SimpleAggregationSqlFieldGenerator _simple;

        public AggregationSqlFieldGenerator(SimpleAggregationSqlFieldGenerator simple)
        {
            this._simple = simple;
        }

	    public bool IsAggregation(BatchItem batch)
	    {
		    return batch.Fields.Any(this.IsAggregation);
	    }

	    private bool IsAggregation(DbField field)
	    {
		    return field.IsAggregation;
	    }

        public string BuildQuery<T>(
            BatchItem batch,
            IResolveFieldContext context,
            SqlFieldResolver<T> resolver,
            string previousFrom,
            string joinTo
        ) where T : SqlFieldResolver<T>, new()
        {
            return this._simple.BuildQuery(batch, context, resolver, previousFrom, joinTo);
        }

        public void GetExtraParams<T>(BatchItem batch, IResolveFieldContext context, SqlFieldResolver<T> resolver, Dictionary<string, DbValue> parameters) where T : SqlFieldResolver<T>, new()
        {
            this._simple.GetExtraParams(batch, context, resolver, parameters);
        }
    }
}
