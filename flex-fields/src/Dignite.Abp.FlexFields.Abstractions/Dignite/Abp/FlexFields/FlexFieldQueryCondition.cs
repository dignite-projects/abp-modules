using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// One condition in a query over flex field values. Fully self-describing: it names the field by
/// <see cref="IFlexFieldData.Id"/> and carries its own <see cref="ValueType"/>, so the query side needs no
/// lookup of the field's definition at all.
/// <para>
/// Identifying the field by id rather than name is what keeps this consistent with the derived index,
/// which also keys by field id. A caller holding only a field name resolves it through its own field
/// model first - the kernel owns no field table to resolve it against.
/// </para>
/// </summary>
public class FlexFieldQueryCondition
{
    /// <summary>
    /// The <see cref="IFlexFieldData.Id"/> being filtered on.
    /// </summary>
    public Guid FieldId { get; set; }

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
}
