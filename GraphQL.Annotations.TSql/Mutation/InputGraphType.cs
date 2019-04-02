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
				typeof(TGraphType)
					.GetProperty(keyValuePair.Key, InputGraphType<TGraphType>._bindingFlags)
					.SetValue(result, keyValuePair.Value);
			}
			return result;
		}
	}
}