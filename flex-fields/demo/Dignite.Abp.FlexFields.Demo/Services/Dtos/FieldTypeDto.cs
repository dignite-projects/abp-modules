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
}
