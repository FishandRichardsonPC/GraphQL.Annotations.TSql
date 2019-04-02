using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Annotations.TSql.Generators;
using GraphQL.Instrumentation;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

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
        public static IServiceCollection AddGraphQLTSql(this IServiceCollection services)
        {
            services.AddSingleton<AggregationSqlFieldGenerator>();
            services.AddSingleton<SimpleAggregationSqlFieldGenerator>();
            services.AddSingleton<StandardSqlFieldGenerator>();

            return services;
        }

        public static TService GetService<TService>(this ResolveFieldContext context)
        {
            return ((IServiceProvider) context.UserContext).GetService<TService>();
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