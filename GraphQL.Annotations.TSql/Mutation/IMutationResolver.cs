using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Mutation
{
	public interface IMutationResolver<out T, in TMutable>
		where T: IObjectGraphType
		where TMutable: IInputObjectGraphType
	{
		T Mutate(ResolveFieldContext context, TMutable input);
	}
}
