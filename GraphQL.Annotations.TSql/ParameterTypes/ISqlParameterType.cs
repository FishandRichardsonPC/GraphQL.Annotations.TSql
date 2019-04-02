
using System.Collections.Generic;

namespace GraphQL.Annotations.TSql.ParameterTypes
{
    public interface ISqlParameterType
    {
        string GetTemplate(IDictionary<string, object> templateParams);
        object GetValue(IDictionary<string, object> templateParams);
    }
}
