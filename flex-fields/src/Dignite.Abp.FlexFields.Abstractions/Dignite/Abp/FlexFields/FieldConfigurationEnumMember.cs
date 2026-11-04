namespace Dignite.Abp.FlexFields;

/// <summary>
/// One member of a <see cref="FieldConfigurationValueKind.Enum"/> configuration key: the integer a
/// configuration stores, and the member's name for a reader who needs to know what it means.
/// </summary>
public sealed record FieldConfigurationEnumMember(int Value, string Name);
