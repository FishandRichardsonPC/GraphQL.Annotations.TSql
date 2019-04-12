using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Annotations.Attributes;
using GraphQL.Annotations.TSql.Generators;
using GraphQL.Annotations.TSql.Mutation;
using GraphQL.Annotations.TSql.Query;
using GraphQL.Instrumentation;
using GraphQL.Resolvers;
using GraphQL.Types;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Type = System.Type;

namespace GraphQL.Annotations.TSql
{
    public static class ExtensionMethods
    {
	    private static List<string> _baseMetadata = new List<string>
	    {
		    "fieldName",
		    "path",
		    "returnTypeName",
		    "sqlTrace",
		    "typeName"
	    };

        // ReSharper disable once InconsistentNaming
        public static IServiceCollection AddGraphQLTSql<TQuery, TMutation>(this IServiceCollection services)
        {
            services.AddSingleton<AggregationSqlFieldGenerator>();
            services.AddSingleton<SimpleAggregationSqlFieldGenerator>();
            services.AddSingleton<StandardSqlFieldGenerator>();

            var addSingleton = typeof(ServiceCollectionServiceExtensions)
                .GetMethods()
                .First(v => v.Name == "AddSingleton" && v.GetGenericArguments().Length == 1 && v.GetParameters().Length == 1);

            foreach (var type in ExtensionMethods.GetMutationTypes<TMutation>())
            {
                addSingleton
                    .MakeGenericMethod(type)
                    .Invoke(services, new object[] { services });
            }

            foreach (var type in ExtensionMethods.GetMutationMethodInfos<TMutation>().Select((v) => v.DeclaringType).Distinct())
            {
	            addSingleton
		            .MakeGenericMethod(type)
		            .Invoke(services, new object[] { services });
	            addSingleton
		            .MakeGenericMethod(typeof(MethodResolver<>).MakeGenericType(type))
		            .Invoke(services, new object[] { services });
            }

            foreach (var type in ExtensionMethods.GetQueryTypes<TQuery>())
            {
	            addSingleton
		            .MakeGenericMethod(type)
		            .Invoke(services, new object[] { services });
	            addSingleton
		            .MakeGenericMethod(typeof(MethodResolver<>).MakeGenericType(type))
		            .Invoke(services, new object[] { services });

	            var mutationResolverArgs = type.GetInterfaces()
		            .FirstOrDefault(v => v.IsGenericType && v.GetGenericTypeDefinition() == typeof(IMutationResolver<,>))?
		            .GenericTypeArguments;
	            if (mutationResolverArgs != null)
	            {
		            addSingleton
			            .MakeGenericMethod(
				            typeof(MutationResolver<,>)
					            .MakeGenericType(mutationResolverArgs)
			            )
			            .Invoke(services, new object[] {services});
	            }
            }

            foreach (var type in ExtensionMethods.GetQueryMethodInfos<TQuery>().Select((v) => v.DeclaringType).Distinct())
            {
	            addSingleton
		            .MakeGenericMethod(type)
		            .Invoke(services, new object[] { services });
	            addSingleton
		            .MakeGenericMethod(typeof(MethodResolver<>).MakeGenericType(type))
		            .Invoke(services, new object[] { services });
            }

            return services;
        }

        // ReSharper disable once InconsistentNaming
        public static ObjectGraphType AddGraphQLTSqlQueryFields<T>(
	        this ObjectGraphType graphType,
	        IServiceProvider serviceProvider
        )
        {
	        foreach (var type in ExtensionMethods.GetQueryTypes<T>())
	        {
		        var field = new FieldType
		        {
			        Name = type.Name.Pluralize(),
			        Arguments = new QueryArguments(Utils.GetArgumentsForType(type, true)),
			        Type = typeof(ListGraphType<>).MakeGenericType(type),
			        Resolver = (IFieldResolver)serviceProvider.GetRequiredService(type)
		        };
		        graphType.AddField(field);
	        }

	        foreach (var method in ExtensionMethods.GetQueryMethodInfos<T>())
	        {
		        graphType.AddField(MethodResolver<T>.GetFieldType(
			        method,
			        method.GetCustomAttribute<GraphMutationMethodAttribute>(),
			        serviceProvider
		        ));
	        }

	        graphType.AddField(
		        new FieldType
		        {
			        Name = "_count",
			        Type = typeof(IntGraphType),
			        Resolver = new CountResolver(graphType)
		        });

	        return graphType;
        }

        // ReSharper disable once InconsistentNaming
        public static ObjectGraphType AddGraphQLTSqlMutationFields<T>(
	        this ObjectGraphType graphType,
	        IServiceProvider serviceProvider
        )
        {
	        foreach (var type in ExtensionMethods.GetQueryTypes<T>())
	        {
		        var typeArgs = type.GetInterfaces()
			        .FirstOrDefault(v => v.IsGenericType && v.GetGenericTypeDefinition() == typeof(IMutationResolver<,>))?
			        .GenericTypeArguments;
		        if (typeArgs != null)
		        {
			        var name = Utils.FirstCharacterToLower(typeArgs[0].Name);
			        var field = new FieldType
			        {
				        Name = name,
				        Arguments = new QueryArguments(
					        new QueryArgument(typeof(NonNullGraphType<>).MakeGenericType(typeArgs[1]))
					        {
						        Name = name
					        }
				        ),
				        Type = type.GetMethod("Mutate")?.ReturnType,
				        Resolver = (IFieldResolver) serviceProvider.GetRequiredService(
					        typeof(MutationResolver<,>).MakeGenericType(typeArgs)
				        )
			        };
			        graphType.AddField(field);
		        }
	        }

	        foreach (var method in ExtensionMethods.GetMutationMethodInfos<T>())
	        {
		        graphType.AddField(MethodResolver<T>.GetFieldType(
			        method,
			        method.GetCustomAttribute<GraphMutationMethodAttribute>(),
			        serviceProvider
		        ));
	        }

	        return graphType;
        }

        private static IEnumerable<MethodInfo> GetQueryMethodInfos<T>()
        {
	        return typeof(T).Assembly.GetTypes()
		        .Where(t => t.IsPublic && !t.IsGenericType && !t.IsAbstract)
		        .SelectMany(
			        t => (
				        t.GetMethods().Where(
					        m => m.GetCustomAttributes(typeof(GraphQueryMethodAttribute)).Any())
			        ))
		        .Concat(
			        typeof(T).Assembly.GetTypes()
				        .Where(t => (
					        t.IsPublic &&
					        !t.IsGenericType &&
					        !t.IsAbstract &&
					        t.GetInterfaces().FirstOrDefault((v) =>(
						        v.IsGenericType &&
						        v.GetGenericTypeDefinition() == typeof(IDeleteResolver<>)
					        )) != null
				        ))
				        .Select(t =>
				        {
					        var resolverType = t.GetInterfaces().First((v) => (
						        v.IsGenericType
						        && v.GetGenericTypeDefinition() == typeof(IDeleteResolver<>)
					        ));
					        return t.GetMethod("Delete", new []
					        {
						        typeof(ResolveFieldContext),
						        resolverType.GetGenericArguments()[0]
					        });
				        })
		        );
        }

        private static IEnumerable<MethodInfo> GetMutationMethodInfos<T>()
        {
	        return typeof(T).Assembly.GetTypes()
		        .Where(t => t.IsPublic && !t.IsGenericType && !t.IsAbstract)
		        .SelectMany(
			        t => (
				        t.GetMethods().Where(
					        m => m.GetCustomAttributes(typeof(GraphMutationMethodAttribute)).Any())
			        ))
		        .Concat(
			        typeof(T).Assembly.GetTypes()
				        .Where(t => (
						    t.IsPublic &&
						    !t.IsGenericType &&
						    !t.IsAbstract &&
						    t.GetInterfaces().FirstOrDefault((v) =>(
								v.IsGenericType &&
								v.GetGenericTypeDefinition() == typeof(IDeleteResolver<>)
							)) != null
						))
						.Select(t =>
				        {
					        var resolverType = t.GetInterfaces().First((v) => (
						        v.IsGenericType
						        && v.GetGenericTypeDefinition() == typeof(IDeleteResolver<>)
					        ));
					        return t.GetMethod("Delete", new []
					        {
						        typeof(ResolveFieldContext),
						        resolverType.GetGenericArguments()[0]
					        });
				        })
		        );
        }

        private static IEnumerable<Type> GetQueryTypes<T>()
        {
	        return typeof(T).Assembly.GetTypes()
		        .Where(t =>
		        {
			        try
			        {
				        return (
					        t.IsPublic
					        && !t.IsGenericType
					        && !t.IsAbstract
					        && t.GetInterfaces().Contains(typeof(IFieldResolver))
					        && t.GetInterfaces().Contains(typeof(IObjectGraphType))
					        && t.IsSubclassOf(typeof(SqlFieldResolver<>).MakeGenericType(t))
					        && t.GetCustomAttribute<GraphQLObjectAttribute>() != null
				        );
			        }
			        catch (ArgumentException)
			        {
				        // This is caused by MakeGenericType when the type would violate the SqlFieldResolver type requirements
				        return false;
			        }
		        });
        }

        private static IEnumerable<Type> GetMutationTypes<T>()
        {
	        return typeof(T).Assembly.GetTypes()
		        .Where(t =>
		        {
			        try
			        {
				        return (
					        t.IsPublic
					        && !t.IsGenericType
					        && !t.IsAbstract
					        && t.GetInterfaces().Contains(typeof(IFieldResolver))
					        && t.GetInterfaces().Contains(typeof(IInputObjectGraphType))
					        && t.IsSubclassOf(typeof(SqlFieldResolver<>).MakeGenericType(t))
					        && t.GetCustomAttribute<GraphQLInputObjectAttribute>() != null
				        );
			        }
			        catch (ArgumentException)
			        {
				        // This is caused by MakeGenericType when the type would violate the SqlFieldResolver type requirements
				        return false;
			        }
		        });
        }

        public static TService GetService<TService>(this ResolveFieldContext context)
        {
	        return ((IServiceProvider) context.UserContext).GetService<TService>();
        }

        public static TService GetRequiredService<TService>(this ResolveFieldContext context)
        {
	        return ((IServiceProvider) context.UserContext).GetRequiredService<TService>();
        }

        public static object GetRequiredService(this ResolveFieldContext context, Type type)
        {
	        return ((IServiceProvider) context.UserContext).GetRequiredService(type);
        }

        public static void EnrichWithTSqlTracing(this ExecutionResult result)
        {
	        var perf = result?.Perf;
	        if (perf == null)
	        {
		        return;
	        }

	        var trace = ExtensionMethods.CreateTrace(perf);
	        if (trace == null)
	        {
		        return;
	        }

	        if (result.Extensions == null)
	        {
		        result.Extensions = new Dictionary<string, object>();
	        }

	        result.Extensions["sqlTracing"] = trace;
        }

        private static object CreateTrace(PerfRecord[] perf)
        {
	        var traces = perf
		        .Where((v) => v.Metadata != null && v.Metadata.ContainsKey("sqlTrace") && (bool) v.Metadata["sqlTrace"])
		        .ToList();

	        if (!traces.Any())
	        {
		        return null;
	        }

	        return traces
		        .GroupBy((v) =>
		        {
			        var path = ((IEnumerable<string>) v.Metadata["path"]).ToList();
			        return v.Metadata.ContainsKey("path") ? String.Join(
				        ".",
				        path.Take(path.Count - 2)
				    ) : null;
		        })
		        .Select((group) => group
					.SelectMany((v) => v.Metadata)
					.Where((v) => !ExtensionMethods._baseMetadata.Contains(v.Key))
					.Append(new KeyValuePair<string, object>(
						"path",
						group.Key.Split('.')
					))
					.GroupBy((v) => v.Key)
					.ToDictionary(
						(v) => v.Key,
						(v) =>
						{
							var items = v.Select((w) => w.Value).ToList();
							if (items.Count == 0)
							{
								return null;
							}
							if (items.Count == 1)
							{
								return items[0];
							}

							return items;
						})
		        )
		        .Where((v) => v.Count > 1);
        }
    }
}