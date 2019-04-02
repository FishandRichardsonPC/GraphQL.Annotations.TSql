using System;
using System.Collections.Generic;
using System.Reflection;
using GraphQL.Annotations.TSql.ParameterTypes;

namespace GraphQL.Annotations.TSql
{
    public struct DbField
    {
        public string RawField;
        public string Field;
        public string ReverseTransform;
        public string Transform;
        public string Alias;
        public bool SkipBuiltins;
        public Type Type;
        public Type GraphType;
        public bool IsTextField;
        public bool IsAggregation;
        public bool IsIdentifierOnly;
    }

    public struct WhereArg
    {
        public DbField Field;
        public string Key;
        public string ReverseTransform;
        public object Value;
    }

    public class BatchItem
    {
        public string Table;
        public IEnumerable<DbField> Fields;
        public IEnumerable<OrderByParameter> OrderBy;
        public bool Descending;
        public IEnumerable<WhereArg> Where;
        public int? Offset = null;
        public int? Count = null;
        public PropertyInfo LocalProperty;
        public string LocalField;
        public string LocalJoinField;
        public string JoinTable;
        public string ForeignJoinField;
        public PropertyInfo ForeignProperty;
        public string ForeignField;
        public string PrimaryProperty;
        public string ExtraCriteriaField;
        public string ExtraCriteriaValue;
        public bool RequiresSubquery => this.Offset != null || this.Count != null;
        public IEnumerable<BatchItem> ChildQueries;
        public string Alias;
        public Type DestType;
        public PropertyInfo SrcProperty;
    }
}
