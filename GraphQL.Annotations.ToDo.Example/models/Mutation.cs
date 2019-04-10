using System;
using GraphQL.Annotations.TSql;
using GraphQL.Types;

namespace GraphQL.Annotations.ToDo.Example.models
{
	public class Mutation: ObjectGraphType
	{
		public Mutation(IServiceProvider serviceProvider)
		{
			this.AddGraphQLTSqlMutationFields<Query>(serviceProvider);
		}
	}
}
