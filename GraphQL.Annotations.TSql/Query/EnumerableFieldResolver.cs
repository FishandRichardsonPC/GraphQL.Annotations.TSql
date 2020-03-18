using System.Collections.Generic;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Query
{
    public abstract class EnumerableFieldResolver<T> : GraphQL.Annotations.Types.ObjectGraphType<T>, IFieldResolver
        where T : class
    {
        public abstract IEnumerable<T> Resolve(IResolveFieldContext context);

        public EnumerableFieldResolver(params object[] injectedParameters): base(injectedParameters)
        {
        }

        object IFieldResolver.Resolve(IResolveFieldContext context)
        {
            return this.Resolve(context);
        }
    }
}
