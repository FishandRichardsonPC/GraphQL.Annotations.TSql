using System;
using GraphQL.Annotations.Attributes;

namespace GraphQL.Annotations.TSql
{
    // ReSharper disable once InconsistentNaming
    /// <summary>Information about a Graph QL Function which needs information about DB Fields</summary>
    public class SqlGraphQLFuncAttribute : GraphQLFuncAttribute
    {
        /// <summary>Any properties which are required for this function to run, this.Property will be set before your method is run</summary>
        public string[] RequiredProperties { get; set; }

        public bool ForwardTypeArguments { get; set; } = false;
    }

    // ReSharper disable once InconsistentNaming
    /// <summary>Information about a Graph QL Field which ties into the DB</summary>
    public class SqlGraphQLFieldAttribute : GraphQLFieldAttribute, ISqlFieldAttribute
    {
        public string DbFieldName { get; set; }
        public string Transform { get; set; }
        public string ReverseTransform { get; set; }
        public bool SkipBuiltins { get; set; }
        public bool IsTextField { get; set; }
        public bool SkipOnInsert { get; set; }
        public bool IsAggregation { get; set; }
    }

    // ReSharper disable once InconsistentNaming
    /// <summary>A many to many relationship without metadata</summary>
    public class SqlGraphQLRelatedAttribute : GraphQLFieldAttribute
    {
        public string LocalProperty;
        public string LocalJoinField;
        public string JoinTable;
        public string ForeignJoinField;
        public string ForeignProperty;
    }

    // ReSharper disable once InconsistentNaming

    // ReSharper disable once InconsistentNaming
    /// <summary>A Graph QL Object which is sourced from the database</summary>
    public class SqlGraphQLObjectAttribute : GraphQLObjectAttribute
    {
        public string ExtraCriteriaField;
        public string ExtraCriteriaValue;
    }

    /// <summary>A Sql Field which is not exposed to the graph</summary>
    public class SqlFieldAttribute : Attribute, ISqlFieldAttribute
    {
        public Type ReturnType { get; set; }
        public string DbFieldName { get; set; }
        public string Transform { get; set; }
        public string ReverseTransform { get; set; }
        public bool SkipBuiltins { get; set; }
        public bool IsTextField { get; set; }
        public bool SkipOnInsert { get; set; }
        public bool IsAggregation { get; } = false;
        public string SqlType { get; set; }
    }

    public interface ISqlFieldAttribute
    {
        Type ReturnType { get; }
        /// <summary>The name in the database to link this field to use</summary>
        string DbFieldName { get; }
        /// <summary>The text tansform to use instead of the default one when querying. Use {0} for the table name and {1} for the field name</summary>
        string Transform { get; }
        /// <summary>The text tansform to use instead of the default one when searching or updating. Use {0} for the incomming parameter</summary>
        string ReverseTransform { get; }
        /// <summary>Skip any builtin transforms, getting the raw values</summary>
        bool SkipBuiltins { get; }
        /// <summary>Is this a TEXT field (instead of a VARCHAR) only applies to strings</summary>
        bool IsTextField { get; }
        /// <summary>Is this an identity field in the database, Make sure this is the PROPERTY name of the primary key</summary>
        bool SkipOnInsert { get; }
        /// <summary>Is this an aggregation field. Internal use only</summary>
        bool IsAggregation { get; }
    }
}
