using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// One condition in a query over flex field values. Fully self-describing: it names the field and carries
/// its own <see cref="ValueType"/>, so the query side needs no lookup of the field's definition at all.
/// <para>
/// It names the field twice because the two persistence shapes address it differently.
/// <see cref="FieldId"/> is the stable identity, and it is what a relational provider's derived index rows
/// key on - so a rename never invalidates a condition there. <see cref="FieldName"/> is the field's key in
/// the value bag itself, which is the only address a provider that queries the bag in place has: a bag holds
/// no field ids. Neither can stand in for the other, and the kernel owns no field table to derive one from.
/// </para>
/// <para>
/// Supplying both costs a caller nothing: whoever builds a condition is already holding the field definition
/// - that is where <see cref="ValueType"/> comes from - so the name is right there too.
/// </para>
/// </summary>
public class FlexFieldQueryCondition
{
    /// <summary>
    /// The <see cref="IFlexFieldData.Id"/> being filtered on.
    /// </summary>
    public Guid FieldId { get; set; }

    /// <summary>
    /// The <see cref="IFlexFieldData.Name"/> being filtered on - the field's key in the value bag.
    /// <para>
    /// Optional: a provider whose derived index keys by field id never reads it. A provider that queries the
    /// bag in place requires it and rejects a condition without one, because there is nothing else to build a
    /// path out of.
    /// </para>
    /// <para>
    /// Pass the field's <i>current</i> name. A field's name is its bag key, so a rename moves the values;
    /// a name that has since been renamed away addresses nothing, whereas <see cref="FieldId"/> survives.
    /// </para>
    /// </summary>
    public string? FieldName { get; set; }

    public FlexFieldQueryOperator Operator { get; set; }

    /// <summary>
    /// Raw comparison value. For <see cref="FlexFieldQueryOperator.In"/>, a comma-separated list.
    /// </summary>
    public string Value { get; set; } = default!;

    public FlexFieldValueType ValueType { get; set; }

    public FlexFieldQueryCondition()
    {
    }

    public FlexFieldQueryCondition(Guid fieldId, FlexFieldQueryOperator @operator, string value, FlexFieldValueType valueType)
    {
        FieldId = fieldId;
        Operator = @operator;
        Value = value;
        ValueType = valueType;
    }

    public FlexFieldQueryCondition(
        Guid fieldId,
        string? fieldName,
        FlexFieldQueryOperator @operator,
        string value,
        FlexFieldValueType valueType)
        : this(fieldId, @operator, value, valueType)
    {
        FieldName = fieldName;
    }
}
