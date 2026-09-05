namespace Dignite.Abp.FlexFields;

/// <summary>
/// One field defined inline as part of a composite field type's own configuration - a <c>Matrix</c>
/// block type's sub-field, or a <c>Table</c>'s column. There is no persisted identity of its own
/// (contrast <see cref="IFlexFieldData"/>, a top-level field's definition, which does), and it is
/// shared verbatim between the two rather than each declaring its own copy.
///
/// <para>
/// Deliberately not just a <see cref="FlexFieldData"/>: that type carries no <c>Required</c> flag,
/// because in the kernel proper "required" is a property of a field's <i>usage</i>
/// (<see cref="FlexFieldValue.Required"/>), not its definition - a definition can be attached to
/// several host types with different Required settings. An inline field has no separate usage record
/// to carry that flag instead, so it lives directly here.
/// </para>
/// </summary>
public class InlineFieldDefinition
{
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Name of the <see cref="IFieldType"/> this field is bound to.</summary>
    public string FieldTypeName { get; set; } = default!;

    public bool Required { get; set; }

    public FieldConfigurationDictionary Configuration { get; set; } = new();
}
