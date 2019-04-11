using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Mutation
{
	public interface IDeleteResolver<in TIdType>
	{
		void Delete(ResolveFieldContext context, TIdType id);
	}
}
