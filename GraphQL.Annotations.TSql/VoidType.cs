using GraphQL.Types;

namespace GraphQL.Annotations.TSql
{
	public class VoidType : BooleanGraphType
	{
		public VoidType()
		{
			this.Name = "void";
			this.Description = "Returns nothing";
		}
	}
}