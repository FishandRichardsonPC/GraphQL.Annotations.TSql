using System;

namespace GraphQL.Annotations.TSql
{
	public class GraphParameterAttribute: Attribute
	{
		public Type GraphType;

		public string Description;
	}
}
