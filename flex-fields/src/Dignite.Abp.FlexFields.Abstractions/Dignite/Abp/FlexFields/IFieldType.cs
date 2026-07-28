using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// A field type: the behavior bound to a field via <see cref="IFlexFieldData.FieldTypeName"/>. Has no
/// rendering concerns on the C# side - it names a field type, not a UI control.
/// </summary>
public interface IFieldType
{
    /// <summary>
    /// Unique registration name of the field type, e.g. "Text".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Display name of the field type.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Slot this field type's searchable values belong in for querying. Null means the field type is not
    /// indexable (e.g. RichText, Matrix).
    /// </summary>
    FlexFieldValueType? IndexValueType { get; }

    /// <summary>
    /// Builds the strongly-typed configuration wrapper for the given configuration dictionary.
    /// </summary>
    FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration);

    /// <summary>
    /// Validates a field's current value and configuration, returning any validation errors.
    /// </summary>
    IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args);

    /// <summary>
    /// Decomposes a field's value into zero, one, or more searchable atomic values: none when the field
    /// type is not indexable, the usage is not searchable, or the value is empty; more than one for
    /// multi-valued field types (e.g. Select, Tree).
    /// <para>
    /// Deliberately provider-neutral - this says only <i>which values are searchable</i>, never anything
    /// about how a provider records them. The EF Core provider pairs each one with
    /// <see cref="IndexValueType"/> and writes it into a typed column; a document store that indexes the
    /// value bag in place can ignore this entirely.
    /// </para>
    /// </summary>
    IEnumerable<object> GetSearchableValues(FlexFieldValue field);
}
