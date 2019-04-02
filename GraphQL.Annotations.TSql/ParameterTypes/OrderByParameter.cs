using GraphQL.Types;

namespace GraphQL.Annotations.TSql.ParameterTypes
{
	public class OrderByParameter: InputObjectGraphType
	{
		public OrderByParameter()
		{
			this.Name = "orderBy";

			this.Field<NonNullGraphType<StringGraphType>>("field");
			this.Field<BooleanGraphType>("descending");
		}

		public new string Field;
		public bool Descending;
	}
}
