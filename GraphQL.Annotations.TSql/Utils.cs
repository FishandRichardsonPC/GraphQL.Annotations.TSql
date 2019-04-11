using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql
{
    public static class Utils
    {
        private static readonly ConcurrentDictionary<Type, IEnumerable<QueryArgument>> RootCache = new ConcurrentDictionary<Type, IEnumerable<QueryArgument>>();
        private static readonly ConcurrentDictionary<Type, IEnumerable<QueryArgument>> NonRootCache = new ConcurrentDictionary<Type, IEnumerable<QueryArgument>>();

        public static IEnumerable<QueryArgument> GetArgumentsForType(Type type, bool isRoot)
        {
            var cache = isRoot ? Utils.RootCache : Utils.NonRootCache;

            if (!cache.ContainsKey(type))
            {
                cache[type] = Utils.InternalGetArgumentsForType(type, isRoot);
            }

            return cache[type];
        }

	    private static IEnumerable<QueryArgument> InternalGetArgumentsForType(Type type, bool isRoot)
	    {
		    var defaultResult = (IEnumerable<QueryArgument>) new QueryArgument[0];
		    var result = defaultResult;
		    try
		    {
			    if (type.IsSubclassOf(typeof(SqlFieldResolver<>).MakeGenericType(type)))
			    {
				    result = (IEnumerable<QueryArgument>) typeof(SqlFieldResolver<>)
					    .MakeGenericType(type).GetMethod("GetArgumentsForType")?
					    .Invoke(null, new object[] {isRoot});
			    }
		    }
		    catch (ArgumentException)
		    {
		    }

		    // ReSharper disable once PossibleUnintendedReferenceComparison
		    if (result == defaultResult)
		    {
			    result = type
				    .GetProperties()
				    .Where((v) => v.GetCustomAttribute(typeof(QueryFieldAttribute)) != null)
				    .Select((v) =>
				    {
					    var attr = v.GetCustomAttribute<QueryFieldAttribute>();
					    return new QueryArgument(
						    (attr.QueryType ?? v.PropertyType).GetGraphTypeFromType()
						)
					    {
						    Name = v.Name
					    };
				    });
		    }

		    return result?.ToList();
	    }

        public static string FirstCharacterToLower(string s)
        {
            return s.Substring(0, 1).ToLower() + s.Substring(1);
        }
    }
}
