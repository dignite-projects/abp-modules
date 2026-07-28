using System;
using System.Collections.Generic;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Configuration item dictionary for fields.
/// </summary>
[Serializable]
public class FieldConfigurationDictionary : Dictionary<string, object>
{
    public FieldConfigurationDictionary()
    {
    }

    public FieldConfigurationDictionary(IDictionary<string, object> dictionary)
        : base(dictionary)
    {
    }
}
