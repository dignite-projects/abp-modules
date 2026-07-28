using System;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// A field value decomposed into one typed, queryable slot - a relational pivot-table row's value shape.
/// Built from one of the raw values <see cref="IFieldType.GetSearchableValues"/> hands back, paired with
/// the field type's own <see cref="IFieldType.IndexValueType"/>.
/// <para>
/// This type is EF Core-specific on purpose: it is the shape of one row of a typed pivot table
/// (<see cref="FlexFieldIndexBase{TEntity}"/>), which is how a relational provider makes "find hosts
/// where field X matches Y" run as SQL instead of in memory. A document-store provider
/// (<c>Dignite.Abp.FlexFields.MongoDb</c>) has no equivalent type - it indexes and queries the value bag
/// (<see cref="FlexFieldDictionary"/>) in place, with no separate pivot representation. This is the design
/// boundary that keeps the two providers apart: nothing relational is welded into
/// <c>Dignite.Abp.FlexFields.Abstractions</c> or <c>Dignite.Abp.FlexFields.Domain</c>.
/// </para>
/// </summary>
public class FlexFieldIndexValue
{
    public FlexFieldValueType ValueType { get; }

    public string? StringValue { get; }

    public decimal? NumberValue { get; }

    public DateTime? DateTimeValue { get; }

    public bool? BooleanValue { get; }

    public Guid? GuidValue { get; }

    private FlexFieldIndexValue(
        FlexFieldValueType valueType,
        string? stringValue = null,
        decimal? numberValue = null,
        DateTime? dateTimeValue = null,
        bool? booleanValue = null,
        Guid? guidValue = null)
    {
        ValueType = valueType;
        StringValue = stringValue;
        NumberValue = numberValue;
        DateTimeValue = dateTimeValue;
        BooleanValue = booleanValue;
        GuidValue = guidValue;
    }

    /// <summary>
    /// Converts a raw searchable value into the slot named by <paramref name="valueType"/>. Keeps type
    /// fidelity on purpose - a number lands in <see cref="NumberValue"/>, never stringified.
    /// </summary>
    public static FlexFieldIndexValue Create(FlexFieldValueType valueType, object rawValue)
    {
        return valueType switch
        {
            FlexFieldValueType.String => new FlexFieldIndexValue(valueType, stringValue: rawValue.ToString()),
            FlexFieldValueType.Number => new FlexFieldIndexValue(valueType, numberValue: Convert.ToDecimal(rawValue)),
            FlexFieldValueType.DateTime => new FlexFieldIndexValue(valueType, dateTimeValue: Convert.ToDateTime(rawValue)),
            FlexFieldValueType.Boolean => new FlexFieldIndexValue(valueType, booleanValue: Convert.ToBoolean(rawValue)),
            FlexFieldValueType.Guid => new FlexFieldIndexValue(valueType, guidValue: rawValue as Guid? ?? Guid.Parse(rawValue.ToString()!)),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType), valueType, null)
        };
    }
}
