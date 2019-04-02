using GraphQL.Server;

namespace GraphQL.Annotations.TSql.AspNetCore
{
    public static class ExtensionMethods
    {
        public static IGraphQLBuilder AddHttpContextUserContextBuilder(this IGraphQLBuilder builder)
        {
            return builder.AddUserContextBuilder(context => new HttpContextUser(context));
        }
    }
}