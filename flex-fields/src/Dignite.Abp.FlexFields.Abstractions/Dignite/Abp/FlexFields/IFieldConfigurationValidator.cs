using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Checks a field definition's configuration against the keys its field type declares
/// (<see cref="IFieldType.ConfigurationKeys"/>): a key the field type does not know, a value of the wrong
/// kind, a required key left out.
/// <para>
/// <b>The kernel measures; the host enforces.</b> There is no definition-save path in the kernel - a field
/// definition is a downstream's own entity - so, like <see cref="CompositeFieldNesting"/>, this is something a
/// host calls where it accepts a configuration: its create and update of a field definition. Call it on what a
/// client sends, <b>never on what is already stored</b>: configuration written before a key was declared must
/// stay readable, and nothing here is on the read path.
/// </para>
/// <para>
/// <b>An update passes what is stored as <c>previous</c>.</b> A client that changes only a field's display name
/// sends the stored configuration back with it, and a definition saved before a key was declared would then be
/// refused for something the client did not touch. With <c>previous</c>, a key whose value is the same as it
/// was, and an inline field that is the same as it was, are not checked again; everything the client added or
/// changed is. Pass it only when the field type is the same as before - a configuration moved to another field
/// type is all new.
/// </para>
/// <para>
/// A field type that declares nothing (<see cref="IFieldType.ConfigurationKeys"/> is null) has none of its own
/// keys checked, and neither is a top-level field type name that is not registered: whether a host may save a
/// definition of a type it does not know is the host's decision. The inline fields of a composite type are
/// held to more: each must name a registered field type.
/// </para>
/// </summary>
public interface IFieldConfigurationValidator
{
    /// <summary>
    /// Every problem with <paramref name="configuration"/> as a configuration of the field type
    /// <paramref name="fieldTypeName"/>, or an empty list when there is none. The composite types' inline fields
    /// (a Matrix block type's sub-fields, a Table's columns) are checked against their own field types too.
    /// <para>
    /// The messages are localized, name the key and the field type, and list the valid keys, so that a client
    /// can correct itself from the message alone. Each result's member name is <c>Configuration[key]</c>.
    /// </para>
    /// </summary>
    /// <param name="fieldTypeName">The field type the configuration is for.</param>
    /// <param name="configuration">What the client sent.</param>
    /// <param name="previous">
    /// The stored configuration of the same field, on an update; null on a create. See the interface remarks.
    /// </param>
    IReadOnlyList<ValidationResult> Validate(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        FieldConfigurationDictionary? previous = null);

    /// <summary>
    /// <see cref="Validate"/>, throwing an <c>AbpValidationException</c> carrying every problem at once when
    /// there are any - one round trip rather than one per mistake.
    /// </summary>
    void Check(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        FieldConfigurationDictionary? previous = null);
}
