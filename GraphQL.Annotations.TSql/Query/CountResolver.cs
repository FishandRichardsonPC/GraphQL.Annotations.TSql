using System;
using System.Linq;
using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.Query
{
	public class CountResolver : IFieldResolver<int?>
	{
		private readonly ObjectGraphType _query;

        private static Exception _argumentException = new ArgumentException("Count can only be queried when the sibling node is a field on Query with a list type of a type that impmements ICountResolver");

		public CountResolver(ObjectGraphType query)
        {
            this._query = query;
        }

		public int? Resolve(ResolveFieldContext context)
		{
			var parent = CountResolver.GetParentNode(context.Document, context.FieldAst);
			var siblings = parent.Children
				.Where(v => v != context.FieldAst)
				.ToList();

			if (
				siblings.Count == 1 &&
				siblings[0] is Field sibling
			)
			{
				var field = this._query.Fields.FirstOrDefault(v => v.Name == sibling.Name);

				if (
					field != null &&
					field.ResolvedType is ListGraphType listType &&
					listType.ResolvedType is ICountResolver resolver
				)
				{
					return resolver.GetCount(
						sibling.Arguments.ToDictionary(
							v => v.Name,
							v => this.GetArgumentValue(v.Value, context.Variables)
						).Where(v => v.Value != null).ToDictionary(
							v => v.Key,
							v => v.Value
						),
						context
					);
				}
				else
				{
					throw CountResolver._argumentException;
				}
			}
			else
			{
				throw CountResolver._argumentException;
			}
		}

		private object GetArgumentValue(IValue arg, Variables vars)
		{
			if (arg is VariableReference varRef)
			{
				return vars.FirstOrDefault(v => v.Name == varRef.Name)?.Value;
			}
			else
			{
				return arg.Value;
			}
		}

		public static INode GetParentNode(INode document, INode node)
		{
			if (document.Children.Contains(node))
			{
				return document;
			}

			return document.Children
				.Select(documentChild => CountResolver.GetParentNode(documentChild, node))
				.FirstOrDefault(result => result != null);
		}

		object IFieldResolver.Resolve(ResolveFieldContext context)
		{
			return this.Resolve(context);
		}
	}
}