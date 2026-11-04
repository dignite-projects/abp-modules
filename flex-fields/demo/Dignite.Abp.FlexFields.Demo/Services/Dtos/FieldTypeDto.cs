namespace Dignite.Abp.FlexFields.Demo.Services.Dtos;

/// <summary>
/// What the client cannot work out for itself about a registered field type.
///
/// <para>
/// The FlexFields kernel ships no application layer, so there is no built-in endpoint serving this - a
/// downstream that needs field type metadata on the client exposes it itself, and
/// <c>ProductFieldAppService.GetFieldTypesAsync</c> is this repository's worked example of that.
/// </para>
///
/// <para>
/// Deliberately not a mirror of <see cref="IFieldType"/>. A field type's label is absent because the
/// Angular library already owns labels: each <c>FieldTypeDefinition</c> carries a <c>displayNameKey</c>
/// the client resolves through its own localization, whereas <see cref="IFieldType.DisplayName"/> is
/// text already localized to the request's culture. Shipping both would give the same label two sources.
/// What the client genuinely cannot derive is <see cref="Indexable"/>.
/// </para>
/// </summary>
public class FieldTypeDto
{
    /// <summary>
    /// The registration key - <see cref="IFieldType.Name"/>, and the value stored in
    /// <c>ProductField.FieldTypeName</c>. Matches the Angular <c>FieldTypeDefinition.name</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether a field of this type can meaningfully be marked <c>Searchable</c> - see
    /// <see cref="FieldTypeExtensions.IsIndexable(IFieldType)"/>. A field designer disables the setting
    /// when this is false; <c>ProductFieldAppService</c> rejects the combination outright.
    /// </summary>
    public bool Indexable { get; set; }

    /// <summary>
    /// The configuration keys this field type understands - <see cref="IFieldType.ConfigurationKeys"/> - or
    /// null when it declares none, in which case its configuration is not checked and nothing can be said
    /// about it. An empty list is the opposite: the field type takes no configuration.
    /// </summary>
    public List<FieldConfigurationKeyDto>? ConfigurationKeys { get; set; }
}

/// <summary>
/// One entry of <see cref="FieldTypeDto.ConfigurationKeys"/>: what a client needs to write the key
/// correctly, so that it does not have to read the repository to learn how.
/// </summary>
public class FieldConfigurationKeyDto
{
    /// <summary>The key as stored, case-sensitive.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value it takes, as <see cref="FieldConfigurationKey.Shape"/> writes it: <c>integer</c>,
    /// <c>integer: 0 (SingleLine) | 1 (MultipleLine)</c>, <c>[{ "text": string, ... }]</c>.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>What the field behaves as when the key is absent; null when there is no fixed default.</summary>
    public object? DefaultValue { get; set; }

    public bool Required { get; set; }
}
