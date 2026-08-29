namespace Dignite.Abp.FlexFields;

/// <summary>
/// Column lengths the domain and every persistence provider have to agree on. Mutable statics (like
/// ABP's own <c>AbpUserConsts</c>) so a host can widen a column before its model is built.
/// </summary>
public static class FlexFieldConsts
{
    /// <summary>Default value: 64. Max length of <c>IFlexField.Name</c>.</summary>
    public static int MaxNameLength { get; set; } = 64;

    /// <summary>Default value: 128. Max length of <c>IFlexField.DisplayName</c>.</summary>
    public static int MaxDisplayNameLength { get; set; } = 128;

    /// <summary>Default value: 64. Max length of <c>IFlexField.FieldTypeName</c>.</summary>
    public static int MaxFieldTypeNameLength { get; set; } = 64;

    /// <summary>
    /// Default value: 512. Max length of a query index row's string slot. The index stores a queryable
    /// projection of a field's value, never the value itself - the authoritative value stays in the
    /// host's own value bag.
    /// </summary>
    public static int MaxStringValueLength { get; set; } = 512;
}
