using GraphQL.Types;

namespace GraphQL.Annotations.TSql.GraphTypes
{
	public class UnionGraphType<T1, T2>: UnionGraphType
		where T1 : IObjectGraphType
		where T2 : IObjectGraphType
	{
		public UnionGraphType()
		{
			this.Type<T1>();
			this.Type<T2>();
		}
	}
	public class UnionGraphType<T1, T2, T3>: UnionGraphType
		where T1 : IObjectGraphType
		where T2 : IObjectGraphType
		where T3 : IObjectGraphType
	{
		public UnionGraphType()
		{
			this.Type<T1>();
			this.Type<T2>();
			this.Type<T3>();
		}
	}
}
