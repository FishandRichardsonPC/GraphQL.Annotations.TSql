using System.Collections.Generic;
using System.Linq;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Mutation
{
	public class MutationResolver<T, TMutable>: IFieldResolver
		where T: IObjectGraphType, IMutationResolver<T, TMutable>
		where TMutable: IInputObjectGraphType, new()
	{
		private readonly T _resolver;

		public MutationResolver(T resolver)
		{
			this._resolver = resolver;
		}

		public object Resolve(ResolveFieldContext context)
		{
			return this._resolver.Mutate(
				context,
				InputGraphType<TMutable>
					.FromDictionary((Dictionary<string, object>)context.Arguments.ToList()[0].Value)
			);
		}
	}
}