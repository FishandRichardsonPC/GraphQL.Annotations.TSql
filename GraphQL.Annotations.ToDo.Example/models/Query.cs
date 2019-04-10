using System;
using GraphQL.Annotations.TSql;
using GraphQL.Types;

namespace GraphQL.Annotations.ToDo.Example.models
{
	public class Query: ObjectGraphType
	{
		public Query(IServiceProvider serviceProvider)
		{
			this.AddGraphQLTSqlQueryFields<Query>(serviceProvider);
		}
	}
}
