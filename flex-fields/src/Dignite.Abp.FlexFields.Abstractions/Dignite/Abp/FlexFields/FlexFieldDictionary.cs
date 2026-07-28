using System;
using System.Collections.Generic;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// The authoritative value bag for a host entity's flex field values (<see cref="IHasFlexFields"/>).
/// Deliberately its own dictionary type, not ABP's <c>ExtraPropertyDictionary</c> - isolation from the
/// shared ExtraProperties bag, not a variant of it (flexfields-design.md §4).
/// </summary>
[Serializable]
public class FlexFieldDictionary : Dictionary<string, object>
{
    public FlexFieldDictionary()
    {
    }

    public FlexFieldDictionary(IDictionary<string, object> dictionary)
        : base(dictionary)
    {
    }
}
