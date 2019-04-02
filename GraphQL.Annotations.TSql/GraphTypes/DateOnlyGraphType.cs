using System;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.GraphTypes
{
	public class DateOnlyGraphType: DateGraphType
	{
		public DateOnlyGraphType()
		{
			this.Name = "DateOnly";
			this.Description =
				"The `DateOnly` scalar type represents a date without a time or timestamp to be formatted " +
				"in accordance with the [ISO-8601](https://en.wikipedia.org/wiki/ISO_8601) standard.";
		}

		public override object Serialize(object value)
		{
			if (!(value is DateTime))
			{
				value = base.ParseValue(value);
			}

			var dateTime = (DateTime?)value;
			return dateTime?.ToString("yyyy-MM-dd");
		}
	}
}
