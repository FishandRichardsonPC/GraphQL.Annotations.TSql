using System.Collections.Generic;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Query
{
	public interface ICountResolver
	{
		int GetCount(IDictionary<string, object> arguments, IResolveFieldContext context);
	}
}
