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
	    /// <inheritdoc/>
        public string DbFieldName { get; set; }
	    /// <inheritdoc/>
        public string Transform { get; set; }
	    /// <inheritdoc/>
        public string ReverseTransform { get; set; }
	    /// <inheritdoc/>
        public bool SkipBuiltins { get; set; }
	    /// <inheritdoc/>
        public bool IsTextField { get; set; }
	    /// <inheritdoc/>
        public bool SkipOnInsert { get; set; }
	    /// <inheritdoc/>
        public bool IsAggregation { get; set; }
    }

    // ReSharper disable once InconsistentNaming
    /// <summary>A many to many relationship without metadata</summary>
    public class SqlGraphQLRelatedAttribute : GraphQLFieldAttribute
    {
	    /// <summary>The property (not field) in this object used for this relationship</summary>
        public string LocalProperty;
	    /// <summary>The field in the join table which the local property maps to in Many-To-Many relationships</summary>
        public string LocalJoinField;
	    /// <summary>The field in the join table used for Many-To-Many relationships</summary>
        public string JoinTable;
        /// <summary>The field in the join table which the foreign property maps to in Many-To-Many relationships</summary>
        public string ForeignJoinField;
        /// <summary>The property (not field) in the other object used for this relationship</summary>
        public string ForeignProperty;
    }

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
	    /// <summary>The type of the field in GraphQL only needed if the automatic type mapping fails</summary>
        public Type ReturnType { get; set; }
        /// <inheritdoc/>
        public string DbFieldName { get; set; }
        /// <inheritdoc/>
        public string Transform { get; set; }
        /// <inheritdoc/>
        public string ReverseTransform { get; set; }
        /// <inheritdoc/>
        public bool SkipBuiltins { get; set; }
        /// <inheritdoc/>
        public bool IsTextField { get; set; }
        /// <inheritdoc/>
        public bool SkipOnInsert { get; set; }
        /// <inheritdoc/>
        public bool IsAggregation { get; } = false;
        /// <summary>The type of the field in SQL only needed if the automatic type mapping fails</summary>
        public string SqlType { get; set; }
    }

    public interface ISqlFieldAttribute
    {
        Type ReturnType { get; }
        /// <summary>The name in the database to link this field to use</summary>
        string DbFieldName { get; }
        /// <summary>
        /// A transform template string which will be used when writing data to the database
        /// {0} will be replaced by the sql parameter
        /// The value you want written to the database should be returned
        /// </summary>
        string Transform { get; }
        /// <summary>
        /// A transform template string which will be used when querying the data from the database
        /// {0} will be replaced by the table, it should be wrapped by [Square Brackets]
        /// {1} will be replaced by the field, it should be wrapped by [Square Brackets]
        /// The value you want exposed on the graph should be returned
        /// </summary>
        string ReverseTransform { get; }
        /// <summary>Skip any builtin transforms, getting the raw values</summary>
        bool SkipBuiltins { get; }
        /// <summary>Is this a TEXT field (instead of a VARCHAR) only applies to strings</summary>
        bool IsTextField { get; }
        /// <summary>Skip this field when inserting into the database</summary>
        bool SkipOnInsert { get; }
        /// <summary>Is this an aggregation field. Internal use only</summary>
        bool IsAggregation { get; }
    }
}
