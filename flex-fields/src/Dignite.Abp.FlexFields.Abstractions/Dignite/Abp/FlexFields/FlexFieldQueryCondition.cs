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
/// Both are required unconditionally - not just when the installed provider happens to need one of them -
/// because a condition is provider-neutral by design and has no way to know which provider will execute it.
/// A caller building one is already holding the field definition (that is where <see cref="ValueType"/>
/// comes from too), so both are on hand regardless.
/// </para>
/// </summary>
public class FlexFieldQueryCondition
{
    /// <summary>
    /// The <see cref="IFlexFieldData.Id"/> being filtered on. What a relational provider's derived index
    /// rows key on.
    /// </summary>
    public Guid FieldId { get; set; }

    /// <summary>
    /// The <see cref="IFlexFieldData.Name"/> being filtered on - the field's key in the value bag, and what
    /// a provider that queries the bag in place addresses by, since a bag holds no field ids.
    /// <para>
    /// Pass the field's <i>current</i> name. A field's name is its bag key, so a rename moves the values;
    /// a name that has since been renamed away addresses nothing, whereas <see cref="FieldId"/> survives.
    /// </para>
    /// </summary>
    public string FieldName { get; set; }

    public FlexFieldQueryOperator Operator { get; set; }

    /// <summary>
    /// Raw comparison value. For <see cref="FlexFieldQueryOperator.In"/>, a comma-separated list.
    /// </summary>
    public string Value { get; set; }

    public FlexFieldValueType ValueType { get; set; }

    public FlexFieldQueryCondition(
        Guid fieldId,
        string fieldName,
        FlexFieldQueryOperator @operator,
        string value,
        FlexFieldValueType valueType)
    {
        FieldId = fieldId;
        FieldName = fieldName;
        Operator = @operator;
        Value = value;
        ValueType = valueType;
    }
}
