using System;
using System.Xml;
using GraphQL.Language.AST;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.GraphTypes
{
	public class IsoTimeSpanGraphType: ScalarGraphType
	{
		public IsoTimeSpanGraphType()
		{
			this.Name = "IsoTimeSpan";
			this.Description =
				"The `IsoTimeSpan` scalar type represents an ISO formatted Timespan " +
				"in accordance with the [ISO-8601](https://en.wikipedia.org/wiki/ISO_8601) standard.";
		}

		public override object Serialize(object value)
		{
			var timeSpan = (TimeSpan?)value;
			return timeSpan == null ? null : XmlConvert.ToString((TimeSpan)timeSpan);
		}

	    public override object ParseValue(object value)
	    {
	        return XmlConvert.ToTimeSpan(value.ToString());
	    }

	    public override object ParseLiteral(IValue value)
	    {
	        return this.ParseValue(value.Value);
	    }
	}
}
