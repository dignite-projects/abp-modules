using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore.ValueComparers;

/// <summary>
/// Change tracking for a field definition's configuration JSON column - same reason as
/// <see cref="FlexFieldDictionaryValueComparer"/>: a value-converted mutable dictionary is otherwise
/// compared by reference, so in-place edits to <see cref="IFlexField.Configuration"/> never persist.
/// </summary>
public class FieldConfigurationDictionaryValueComparer : ValueComparer<FieldConfigurationDictionary>
{
    public FieldConfigurationDictionaryValueComparer()
        : base(
            (a, b) => Compare(a, b),
            d => d.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.GetHashCode())),
            d => new FieldConfigurationDictionary(d))
    {
    }

    private static bool Compare(FieldConfigurationDictionary? a, FieldConfigurationDictionary? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null)
        {
            return b is null;
        }

        if (b is null)
        {
            return false;
        }

        return a.SequenceEqual(b);
    }
}
