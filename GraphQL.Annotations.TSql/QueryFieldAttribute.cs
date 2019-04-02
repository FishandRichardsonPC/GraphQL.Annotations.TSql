using System;

namespace GraphQL.Annotations.TSql
{
	public class QueryFieldAttribute: Attribute
	{
		public Type QueryType;

		public bool Required = false;
	}
}
