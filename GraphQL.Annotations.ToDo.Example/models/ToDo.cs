using System;
using GraphQL.Annotations.Attributes;
using GraphQL.Annotations.TSql;
using GraphQL.Annotations.TSql.Mutation;
using GraphQL.Types;

namespace GraphQL.Annotations.ToDo.Example.models
{
	public class ToDoBase<T>: SqlFieldResolver<T> where T : SqlFieldResolver<T>, new()
	{
		[SqlGraphQLField(ReturnType = typeof(IdGraphType), SkipOnInsert = true)]
		public Guid? Id { get; set; }
		[SqlGraphQLField]
		public string Text { get; set; }
		[SqlGraphQLField]
		public Priority? Priority { get; set; }
		[SqlGraphQLField(ReturnType = typeof(DateGraphType))]
		public DateTime? DueDate { get; set; }
		[SqlGraphQLField]
		public bool? Completed { get; set; }

		public override string Table => "ToDo";
		public override string DefaultOrder => "DueDate";
		public override string PrimaryProperty => "Id";
	}

	[GraphQLObject]
	public class ToDo :
		ToDoBase<ToDo>,
		IObjectGraphType,
		IMutationResolver<ToDo, ToDoMutable>,
		IDeleteResolver<Guid>
	{
		public ToDo Mutate(IResolveFieldContext context, ToDoMutable input)
		{
			return SqlFieldMutator.Mutate<ToDo, ToDoMutable>(context, input);
		}

		public void Delete(IResolveFieldContext context, Guid id)
		{
			SqlFieldMutator.Delete<ToDoMutable, Guid>(context, id);
		}
	}

	[GraphQLInputObject]
	public class ToDoMutable : ToDoBase<ToDoMutable>, IInputObjectGraphType
	{
	}
}
