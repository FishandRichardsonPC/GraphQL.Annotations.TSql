using System;

namespace GraphQL.Annotations.TSql
{
	public class CountResolverException: Exception
	{
		public CountResolverException(string message) : base(message)
		{

		}
	}
}
