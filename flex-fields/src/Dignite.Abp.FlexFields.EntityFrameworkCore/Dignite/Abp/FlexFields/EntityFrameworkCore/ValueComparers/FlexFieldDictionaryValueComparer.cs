using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore.ValueComparers;

/// <summary>
/// Change tracking for the value bag's JSON column. Without this, EF Core compares the converted
/// <see cref="FlexFieldDictionary"/> by reference and never notices an in-place mutation
/// (<c>SetField</c>/<c>RemoveField</c> on a loaded entity), so the update is silently dropped.
/// Mirrors ABP's own <c>ExtraPropertyDictionaryValueComparer</c>.
/// </summary>
public class FlexFieldDictionaryValueComparer : ValueComparer<FlexFieldDictionary>
{
    public FlexFieldDictionaryValueComparer()
        : base(
            (a, b) => Compare(a, b),
            d => d.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.GetHashCode())),
            d => new FlexFieldDictionary(d))
    {
    }

    private static bool Compare(FlexFieldDictionary? a, FlexFieldDictionary? b)
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
