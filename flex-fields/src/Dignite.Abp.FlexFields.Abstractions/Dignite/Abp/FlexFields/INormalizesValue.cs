namespace Dignite.Abp.FlexFields;

/// <summary>
/// A field type whose value has a canonical wire shape it can re-derive from any structurally valid
/// input - the composite types <c>Matrix</c> and <c>Table</c>, whose values are lists of composite
/// objects with a fixed casing convention (camelCase, matching every field type's own
/// <c>JsonSerializerDefaults.Web</c> read path) that nothing otherwise enforces on write.
/// <para>
/// <b>Why this has to exist.</b> <see cref="IFieldType.Validate"/> already parses a composite value
/// case-insensitively to check its shape, but returns only errors - never the parsed value - so a
/// structurally valid value with the "wrong" key casing (e.g. <c>BlockTypeName</c> instead of
/// <c>blockTypeName</c>) passes validation and is then persisted <i>exactly as received</i>: a host
/// writes what it was handed straight into <see cref="IHasFlexFields.FlexFields"/>. Every reader
/// downstream - the Angular field-type controls in particular - expects the camelCase shape, so a value
/// that round-tripped through validation without this normalization step can be silently unreadable on
/// the client despite having saved without error.
/// </para>
/// <para>
/// <b>Deliberately separate from <see cref="IFieldType.Validate"/>.</b> Validation answers "is this
/// value acceptable"; normalization answers "what is the one canonical way to store an acceptable
/// value" - conflating them would mean every future <c>Validate</c> override also has to remember to
/// double as a value transform. It is the host that calls this, on the write path, just before
/// validating, and only for the field types that opt in - see the demo's <c>ProductAppService</c> for
/// the worked example. Not a kernel-wide concept, because none of the scalar built-ins
/// (Text/Number/DateTime/Select/Boolean/Tree) has a composite value that needs canonicalizing.
/// </para>
/// </summary>
public interface INormalizesValue
{
    /// <summary>
    /// Returns the canonical form of <paramref name="value"/> to store, or <paramref name="value"/>
    /// itself unchanged if it is not a shape this field type recognizes - normalization never masks a
    /// structural problem <see cref="IFieldType.Validate"/> would otherwise report, it only re-cases an
    /// already-valid value.
    /// </summary>
    object? Normalize(object? value);
}
