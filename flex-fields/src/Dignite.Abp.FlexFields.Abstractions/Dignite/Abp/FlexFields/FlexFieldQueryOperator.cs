namespace Dignite.Abp.FlexFields;

/// <summary>
/// Comparison a <see cref="FlexFieldQueryCondition"/> applies against a field's indexed value.
/// </summary>
public enum FlexFieldQueryOperator
{
    Equals,

    NotEquals,

    /// <summary>
    /// Substring match against the field's indexed string slot.
    /// </summary>
    Contains,

    GreaterThan,

    GreaterThanOrEqual,

    LessThan,

    LessThanOrEqual,

    /// <summary>
    /// Matches when the indexed value is any of a comma-separated set carried in <see cref="FlexFieldQueryCondition.Value"/>.
    /// </summary>
    In
}
