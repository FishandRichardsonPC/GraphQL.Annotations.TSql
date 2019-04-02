using System;

namespace GraphQL.Annotations.TSql
{
    public class MethodAttribute: Attribute
    {
	    public Type ReturnGraphType { get; set; }
        public bool NameSuffix { get; set; } = true;
    }
}