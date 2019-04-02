using System.Collections.Generic;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Query
{
    public abstract class EnumerableFieldResolver<T> : GraphQL.Annotations.Types.ObjectGraphType<T>, IFieldResolver
        where T : class
    {
        public abstract IEnumerable<T> Resolve(ResolveFieldContext context);

        public EnumerableFieldResolver(params object[] injectedParameters): base(injectedParameters)
        {
        }

        object IFieldResolver.Resolve(ResolveFieldContext context)
        {
            return this.Resolve(context);
        }
    }
}
