using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GraphQL.Annotations.TSql.Mutation;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQL.Annotations.TSql
{
	public static class ObjectExtensions
	{
		public static T ToObject<T>(this IDictionary<string, object> source)
			where T : class, new()
		{
			var someObject = new T();
			var someObjectType = someObject.GetType();

			foreach (var item in source)
			{
				someObjectType
					.GetProperty(item.Key, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase)?
					.SetValue(someObject, item.Value, null);
			}

			return someObject;
		}

		public static IEnumerable<T> ToObjects<T>(this IEnumerable<IDictionary<string, object>> sources)
			where T : class, new()
		{
			return sources.Select((v) => v.ToObject<T>());
		}

		public static IDictionary<string, object> AsDictionary(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
		{
			return source.GetType().GetProperties(bindingAttr).ToDictionary
			(
				propInfo => propInfo.Name,
				propInfo => propInfo.GetValue(source, null)
			);

		}
	}

	public class MethodResolver<TResolver>: IFieldResolver
	{
		private readonly TResolver _resolver;
		private BindingFlags _bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

		public MethodResolver(TResolver resolver)
		{
			this._resolver = resolver;
		}

		public object Resolve(ResolveFieldContext context)
		{
			var fieldName = context.FieldName;
			fieldName = new Regex("^" + typeof(TResolver).Name + "_", RegexOptions.IgnoreCase).Replace(
				fieldName,
				""
			);
			var method = typeof(TResolver).GetMethod(fieldName, this._bindingFlags);
			if (method == null)
			{
				method = typeof(TResolver)
					.GetMethods()
					.First((v) => v.GetCustomAttribute<MethodAttribute>()?.NameSuffix == false);
			}
			var args = context.Arguments.ToList();
			return method.Invoke(
				this._resolver,
				method.GetParameters().Select(
					v =>
					{
						if (v.ParameterType == typeof(ResolveFieldContext))
						{
							return context;
						}

						var value = args.FirstOrDefault(w => w.Key.ToLower() == v.Name.ToLower())
							.Value;

						if (value == null)
						{
							return value;
						}

						if (value.GetType() == typeof(Dictionary<string, object>))
						{
							var fromDict = typeof(InputGraphType<>).MakeGenericType(v.ParameterType)
								.GetMethod("FromDictionary");
							if (fromDict != null)
							{
								return fromDict.Invoke(null, new[] {value});
							}
						}
						else if (value is IList list)
						{
							MethodInfo cast;
							if (list.Count > 0 && list[0] is Dictionary<string, object>)
							{
								value = list.Cast<Dictionary<string, object>>();
								cast = typeof(ObjectExtensions)
									.GetMethod("ToObjects")?
									.MakeGenericMethod(
										v.ParameterType.ContainsGenericParameters
											? v.ParameterType.GenericTypeArguments[0]
											: v.ParameterType.GetElementType()
									);
							}
							else
							{
								cast = typeof(Enumerable)
									.GetMethod("Cast")?
									.MakeGenericMethod(
										v.ParameterType.ContainsGenericParameters
											? v.ParameterType.GenericTypeArguments[0]
											: v.ParameterType.GetElementType()
									);
							}

							var castList = cast?.Invoke(value, new[] {value});
							if (v.ParameterType.IsArray)
							{
								var toArray = typeof(Enumerable)
									.GetMethod("ToArray")?
									.MakeGenericMethod(v.ParameterType.GetElementType());
								return toArray?.Invoke(castList, new[] {castList});
							}
							else
							{
								return castList;
							}
						} else if (v.ParameterType == typeof(Guid) || v.ParameterType == typeof(Guid?))
						{
							return Guid.Parse(value.ToString());
						}

						return value;
					}).ToArray()
			);
		}

		public static FieldType GetFieldType(
			MethodInfo method,
			MethodAttribute methodAttr,
			IServiceProvider serviceProvider
		)
		{
			var type = new FieldType();
			type.Name = methodAttr?.NameSuffix != true ?
				Utils.FirstCharacterToLower(method.DeclaringType?.Name) + '_' + Utils.FirstCharacterToLower(method.Name) :
				Utils.FirstCharacterToLower(method.DeclaringType?.Name);
			type.Arguments = new QueryArguments(
				method.GetParameters()
					.Where(p => p.ParameterType != typeof(ResolveFieldContext))
					.Select(p =>
					{
						var attr = p.GetCustomAttribute<GraphParameterAttribute>();

						return new QueryArgument(
							attr?.GraphType ?? (
								p.ParameterType.IsGraphType()
									? typeof(InputGraphType<>).MakeGenericType(p.ParameterType)
									: (
										p.ParameterType.IsEnum
											? typeof(EnumerationGraphType<>).MakeGenericType(p.ParameterType)
											: (
												p.ParameterType == typeof(Guid) || p.ParameterType == typeof(Guid?)
													? typeof(StringGraphType)
													: p.ParameterType.GetGraphTypeFromType()
											)
									)
							)
						)
						{
							Name = Utils.FirstCharacterToLower(p.Name),
							Description = attr?.Description
						};
					})
			);
			type.Type = methodAttr?.ReturnGraphType ?? (
				method.ReturnType.IsGraphType() ? method.ReturnType : (
					method.ReturnType == typeof(void) ?
						typeof(VoidType) :
						method.ReturnType.GetGraphTypeFromType()
				)
			);
			type.Resolver = (IFieldResolver)serviceProvider.GetRequiredService(
				typeof(MethodResolver<>).MakeGenericType(method.DeclaringType)
			);
			return type;
		}
    }
}