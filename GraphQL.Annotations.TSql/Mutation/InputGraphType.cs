using System;
using System.Collections.Generic;
using System.Reflection;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Mutation
{
	public static class InputGraphType<TGraphType> where TGraphType : IInputObjectGraphType, new()
	{
		private static BindingFlags _bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

		public static TGraphType FromDictionary(Dictionary<string, object> d)
		{
			var result = new TGraphType();
			foreach (var keyValuePair in d)
			{
				var prop = typeof(TGraphType)
					.GetProperty(keyValuePair.Key, InputGraphType<TGraphType>._bindingFlags);
				if (prop != null)
				{
					var value = keyValuePair.Value;

					if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
					{
						value = value == null ? null : (Guid?)Guid.Parse(value.ToString());
					}

					prop.SetValue(result, value);
				}
			}
			return result;
		}
	}
}