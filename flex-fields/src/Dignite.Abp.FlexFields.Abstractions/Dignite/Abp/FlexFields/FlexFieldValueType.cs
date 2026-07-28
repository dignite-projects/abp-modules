namespace Dignite.Abp.FlexFields;

/// <summary>
/// Which typed slot a field's value belongs in: it is how a provider types the values
/// <see cref="IFieldType.GetSearchableValues"/> hands back, and it describes a
/// <see cref="FlexFieldQueryCondition.Value"/> so the query side can compare against that same slot.
/// </summary>
public enum FlexFieldValueType
{
    String,
    Number,
    DateTime,
    Boolean,
    Guid
}
