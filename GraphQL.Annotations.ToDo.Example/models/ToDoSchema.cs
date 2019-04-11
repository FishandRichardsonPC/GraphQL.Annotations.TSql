using GraphQL.Types;

namespace GraphQL.Annotations.ToDo.Example.models
{
	public class ToDoSchema: Schema
	{
		public ToDoSchema(Query query, Mutation mutation)
		{
			this.Query = query;
			this.Mutation = mutation;
		}
	}
}
