using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Annotations.Attributes;
using GraphQL.Annotations.Types;

namespace GraphQL.Annotations.TSql.ParameterTypes
{
    [GraphQLObject(Name = "SqlDateTime", Description = "Provide exactly one property")]
    public class SqlDateTimeParameter: SqlRangeParameter<SqlDateTimeParameter, DateTime?> {}

    [GraphQLObject(Name = "SqlDate", Description = "Provide exactly one property")]
    public class SqlDateParameter : SqlRangeParameter<SqlDateParameter, DateTime?>
    {
        public override string GetTemplate(IDictionary<string, object> templateParams)
        {
            return base.GetTemplate(templateParams, "cast({0} as date)");
        }
    }

    [GraphQLObject(Name = "SqlInt", Description = "Provide exactly one property")]
    public class SqlIntParameter: SqlRangeParameter<SqlIntParameter, int?> {}

    public class SqlRangeParameter<T, TParamType>: InputObjectGraphType<T>, ISqlParameterType
        where T: SqlRangeParameter<T, TParamType>
    {
        [GraphQLField(Description = "Equals, also supports null values")]
        public TParamType Eq { get; set; }
        [GraphQLField(Description = "Not equals, also supports null values")]
        public TParamType Ne { get; set; }
        [GraphQLField(Description = "Greater than")]
        public TParamType Gt { get; set; }
        [GraphQLField(Description = "Less than")]
        public TParamType Lt { get; set; }
        [GraphQLField(Description = "Greater than or equal")]
        public TParamType Gte { get; set; }
        [GraphQLField(Description = "Less than or equal")]
        public TParamType Lte { get; set; }
        [GraphQLField(Description = "Between two values, including the ends")]
        public TParamType[] InI { get; set; }
        [GraphQLField(Description = "Between two values, excluding the ends")]
        public TParamType[] InE { get; set; }
        [GraphQLField(Description = "Outside two values, including the ends")]
        public TParamType[] OutI { get; set; }
        [GraphQLField(Description = "Outside two values, excluding the ends")]
        public TParamType[] OutE { get; set; }

        private string[] _paramOptions;

        public SqlRangeParameter()
        {
            this._paramOptions = this.GetType().GetProperties().Select(p => Utils.FirstCharacterToLower(p.Name)).ToArray();
        }

        public virtual string GetTemplate(IDictionary<string, object> templateParams)
        {
            return this.GetTemplate(templateParams, "{0}");
        }

        protected string GetTemplate(IDictionary<string, object> templateParams, string template)
        {
            var item = templateParams.FirstOrDefault();
            if (templateParams.Count != 1 || !this._paramOptions.Contains(item.Key))
            {
                throw new ArgumentException("You must supply exactly one of " + String.Join(", ", this._paramOptions));
            }

            var value = String.Format(template, "{0}");
            var itemRaw = String.Format(template, "{1}");
            var itemZero = String.Format(template, "{1}_0");
            var itemOne = String.Format(template, "{1}_1");

            switch (item.Key)
            {
                case "eq":
                    if (item.Value == null)
                    {
                        return $"{value} IS NULL";
                    }
                    return $"{value} = {itemRaw}";
                case "ne":
                    if (item.Value == null)
                    {
                        return $"{value} IS NOT NULL";
                    }
                    return $"{value} != {itemRaw}";
                case "gt":
                    return $"{value} > {itemRaw}";
                case "lt":
                    return $"{value} < {itemRaw}";
                case "gte":
                    return $"{value} >= {itemRaw}";
                case "lte":
                    return $"{value} <= {itemRaw}";
                case "inI":
                    return $"({itemZero} <= {value} AND {value} <= {itemOne})";
                case "inE":
                    return $"({itemZero} < {value} AND {value} < {itemOne})";
                case "outI":
                    return $"({itemZero} >= {value} OR {value} >= {itemOne})";
                case "outE":
                    return $"({itemZero} > {value} OR {value} > {itemOne})";
                default:
                    throw new ArgumentException();
            }
        }

        public object GetValue(IDictionary<string, object> templateParams)
        {
            return templateParams.First().Value;
        }
    }
}
