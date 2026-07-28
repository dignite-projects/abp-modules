using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// A flex field's <b>definition</b>, as a plain data contract: identity, the field type it is bound to,
/// and that field type's configuration. Free of any persistence or DDD vocabulary - the DDD-free mirror
/// of <c>IFlexField</c> (<c>Dignite.Abp.FlexFields.Domain</c>), the same relationship ABP's
/// <c>IUserData</c> has to <c>IUser</c>.
/// <para>
/// A definition carries no value and no per-usage flags: the same field can be attached to several
/// host types with different Required/Searchable settings. Definition + those flags + the value read
/// from the host's bag are merged at runtime into a <see cref="FlexFieldValue"/>.
/// </para>
/// </summary>
public interface IFlexFieldData
{
    Guid Id { get; }

    /// <summary>
    /// Unique name of the field. This is the key used to read/write the field's value in the host's
    /// value bag (<see cref="IHasFlexFields.FlexFields"/>).
    /// </summary>
    string Name { get; }

    string DisplayName { get; }

    string? Description { get; }

    /// <summary>
    /// Name of the <see cref="IFieldType"/> this field is bound to (<see cref="IFieldType.Name"/>).
    /// </summary>
    string FieldTypeName { get; }

    /// <summary>
    /// Configuration of the field, interpreted by its field type.
    /// </summary>
    FieldConfigurationDictionary Configuration { get; }
}
