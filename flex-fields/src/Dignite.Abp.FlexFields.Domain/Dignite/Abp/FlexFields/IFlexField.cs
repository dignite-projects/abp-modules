using System;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// A flex field's <b>definition</b> as an Entity: identity, the field type it is bound to, and that
/// field type's configuration. Free of any storage vocabulary beyond aggregate-root identity - a
/// downstream host's own entity implements this (e.g. a CMS's <c>Field</c>), and the EF Core provider's
/// <c>ConfigureFlexField&lt;TField&gt;()</c> maps exactly these members to columns.
/// <para>
/// This is an Entity contract, which is why it lives here rather than in
/// <c>Dignite.Abp.FlexFields.Abstractions</c>: that project is deliberately free of
/// <c>Volo.Abp.Ddd.Domain</c>, the same layering ABP's own <c>Volo.Abp.Users</c> uses for <c>IUser</c>
/// (Domain) versus <c>IUserData</c> (Abstractions). A downstream converts its own <see cref="IFlexField"/>
/// entity to the DDD-free <see cref="FlexFieldData"/> via <c>ToFlexFieldData()</c> before handing it to the
/// kernel as part of a <see cref="FlexFieldValue"/>.
/// </para>
/// <para>
/// A definition carries no value and no per-usage flags: the same field can be attached to several
/// host types with different Required/Searchable settings. Definition + those flags + the value read
/// from the host's bag are merged at runtime into a <see cref="FlexFieldValue"/>.
/// </para>
/// </summary>
public interface IFlexField : IAggregateRoot<Guid>
{
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
