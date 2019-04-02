using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Annotations.Attributes;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql
{
	public class ObjectGraphType<T> : ComplexGraphType<T>
		where T : ObjectGraphType<T>, new()
	{
		private static ConcurrentDictionary<Type, IEnumerable<FieldType>> FieldCache = new ConcurrentDictionary<Type, IEnumerable<FieldType>>();

		public ObjectGraphType(params object[] injectedParameters)
		{
			this.ApplyTypeData<T>();
			this.BuildFields(injectedParameters);
			this.ImplementInterfaces();
		}

		private void BuildFields(object[] injectedParameters)
		{
			if (!ObjectGraphType<T>.FieldCache.ContainsKey(typeof(T)))
			{
				this.ApplyProperties<T>();
				this.ApplyMethods<T>(injectedParameters, true);
				ObjectGraphType<T>.FieldCache[typeof(T)] = this.Fields.ToList();
			}
			else
			{
				foreach (var fieldType in ObjectGraphType<T>.FieldCache[typeof(T)])
				{
					this.AddField(fieldType);
				}
			}
		}

		private void ImplementInterfaces()
		{
			var type = typeof(T);
			this.Interfaces = ObjectGraphType<T>
				.GetBaseTypes(type)?
				.Where(t => t.GetTypeInfo().IsAbstract)
				.Concat(System.Reflection.TypeExtensions.GetInterfaces(type))
				.Select(t => t.GetGraphTypeFromAttribute<GraphQLInterfaceAttribute>())
				.Where(t => t != null);
		}

		private static IEnumerable<Type> GetBaseTypes(Type type)
		{
			while ((object) (type = type.GetTypeInfo().BaseType) != null)
			{
				yield return type;
			}
		}

		public IEnumerable<Type> Interfaces { get; set; } = new List<Type>();

		public IEnumerable<IInterfaceGraphType> ResolvedInterfaces { get; set; } =
			new List<IInterfaceGraphType>();

		public void AddResolvedInterface(IInterfaceGraphType graphType)
		{
			this.ResolvedInterfaces = this.ResolvedInterfaces.Concat(new[] {graphType});
		}

		public Func<object, bool> IsTypeOf { get; set; }
	}
}
