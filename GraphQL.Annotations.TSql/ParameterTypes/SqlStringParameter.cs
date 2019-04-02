using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Annotations.Attributes;
using GraphQL.Types;

namespace GraphQL.Annotations.TSql.ParameterTypes
{
	[GraphQLObject(Description = "Provide text, (optional) begins, and exactly one other property")]
	public class LevenshteinParameters : GraphQL.Annotations.Types.InputObjectGraphType<LevenshteinParameters>
	{
		[GraphQLField(ReturnType = typeof(NonNullGraphType<StringGraphType>))]
		public string Text { get; set; }
		[GraphQLField(Description = "Enabled a mode where only the first n characters are compared where n is the smaller length + 2")]
		public bool? Begins { get; set; }
		[GraphQLField(Description = "Equals, also supports null values")]
		public int? Eq { get; set; }
		[GraphQLField(Description = "Not equals, also supports null values")]
		public int? Ne { get; set; }
		[GraphQLField(Description = "Greater than")]
		public int? Gt { get; set; }
		[GraphQLField(Description = "Less than")]
		public int? Lt { get; set; }
		[GraphQLField(Description = "Greater than or equal")]
		public int? Gte { get; set; }
		[GraphQLField(Description = "Less than or equal")]
		public int? Lte { get; set; }
	}

    [GraphQLObject(Name = "SqlString", Description = "Provide exactly one property")]
    public class SqlStringParameter: GraphQL.Annotations.Types.InputObjectGraphType<SqlStringParameter>, ISqlParameterType
    {
        [GraphQLField(Description = "Equals, also supports null values")]
        public string Eq { get; set; }
        [GraphQLField(Description = "Not Equals, also supports null values")]
        public string Ne { get; set; }
        [GraphQLField]
        public string Like { get; set; }
	    [GraphQLField]
	    public string NotLike { get; set; }
	    [GraphQLField(Description = "Find records based on the Levenshtein distance, see https://en.wikipedia.org/wiki/Levenshtein_distance")]
	    public LevenshteinParameters Ld { get; set; }
	    [GraphQLField]
	    public IEnumerable<SqlStringParameter> And { get; set; }
	    [GraphQLField]
	    public IEnumerable<SqlStringParameter> Or { get; set; }

	    private string[] _paramOptions;
	    private string[] _ltParamOptions;

        public SqlStringParameter()
        {
            this._paramOptions = this
	            .GetType()
	            .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
	            .Select(p => Utils.FirstCharacterToLower(p.Name))
	            .ToArray();
	        this._ltParamOptions = typeof(LevenshteinParameters)
		        .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
		        .Select(p => Utils.FirstCharacterToLower(p.Name))
		        .Where(v => v != "text")
		        .Where(v => v != "begins")
		        .ToArray();
        }

	    public string GetTemplate(IDictionary<string, object> templateParams)
	    {
		    var subField = -1;
		    return this.GetTemplate(templateParams, ref subField);
	    }

	    private string GetTemplate(IDictionary<string, object> templateParams, ref int subField)
	    {
		    var item = templateParams.FirstOrDefault();
		    if (templateParams.Count != 1 || !this._paramOptions.Contains(item.Key))
		    {
			    throw new ArgumentException(
				    "You must supply exactly one of " + String.Join(", ", this._paramOptions));
		    }

		    if (item.Key == "and" || item.Key == "or")
		    {
			    if (item.Value == null)
			    {
				    throw new ArgumentException("And and Or require a value to be supplied");
			    }

			    var value = ((List<object>) item.Value);
			    var templates = new List<string>();
			    foreach (var v in value)
			    {
				    subField++;
				    // The ref makes it so we can't use linq here
				    templates.Add(this.GetTemplate((IDictionary<string, object>) v, ref subField));
			    }

			    return "(("
				    + String.Join(
					    item.Key == "and" ? ") AND (" : ") OR (",
					    templates
				    )
				    + "))";
		    }

		    if (item.Value == null && (item.Key == "like" || item.Key == "notLike"))
		    {
			    throw new ArgumentException("You cannot search like null, use equals or not equals");
		    }

		    if (item.Value == null)
		    {
			    return "{0} IS " + (item.Key == "eq" ? "" : "NOT ") + " NULL";
		    }

		    if (item.Key == "like" || item.Key == "notLike")
		    {
			    return "{0} " + (item.Key == "like" ? "" : "NOT ") + " LIKE {1}" + (subField > -1 ? $"_{subField}" : "");
		    }

		    if (item.Key == "ld")
		    {
			    if (subField == -1)
			    {
				    subField = 0;
			    }

			    subField++;

			    var subItems = ((Dictionary<string, object>) item.Value)
				    .Where(v => v.Key != "text")
				    .Where(v => v.Key != "begins")
				    .Where(v => v.Value != null)
				    .ToList();

			    var beginsValue = ((Dictionary<string, object>) item.Value)
				    .FirstOrDefault(v => v.Key == "begins")
				    .Value;

			    var isBegins = false;

			    if (beginsValue is bool begins)
			    {
				    isBegins = begins;
			    }

			    if (subItems.Count != 1)
			    {
				    throw new ArgumentException(
					    "You must supply exactly one of " + String.Join(", ", this._ltParamOptions));
			    }

			    var subItem = subItems.First();

			    var result = "master.dbo.edit_distance({0}, {1}" + $"_{subField - 1}, {(isBegins ? '1' : '0')}) ";
			    var subItemValue = "{1}_" + subField;

			    switch (subItem.Key)
			    {
				    case "eq":
					    return $"{result} = {subItemValue}";
				    case "ne":
					    return $"{result} != {subItemValue}";
				    case "gt":
					    return $"{result} > {subItemValue}";
				    case "lt":
					    return $"{result} < {subItemValue}";
				    case "gte":
					    return $"{result} >= {subItemValue}";
				    case "lte":
					    return $"{result} <= {subItemValue}";
				    default:
					    throw new ArgumentException("You must supply exactly one of " + String.Join(", ", this._ltParamOptions));
			    }
		    }

		    return "{0} " + (item.Key == "eq" ? "" : "!") + "= {1}" + (subField > -1 ? $"_{subField}" : "");
	    }

	    public object GetValue(IDictionary<string, object> templateParams)
	    {
		    var item = templateParams.First();
		    if (item.Key == "and" || item.Key == "or")
		    {
			    return ((List<object>) item.Value)
				    .Select(v => this.GetValue((IDictionary<string, object>) v))
				    .ToArray();
		    }

		    if (item.Key == "ld")
		    {
			    var items = (Dictionary<string, object>) item.Value;
			    var subItem = items
				    .Where(v => v.Key != "text")
				    .Where(v => v.Key != "begins")
				    .First(v => v.Value != null);

			    return new[]
			    {
				    items["text"],
				    subItem.Value
			    };
		    }

		    return item.Value;
	    }
    }
}
