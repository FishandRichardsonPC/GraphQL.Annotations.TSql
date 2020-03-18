using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Mutation
{
	public interface IMutationResolver<out T, in TMutable>
		where T: IObjectGraphType, IMutationResolver<T, TMutable>
		where TMutable: IInputObjectGraphType, new()
	{
		T Mutate(IResolveFieldContext context, TMutable input);
	}
}
