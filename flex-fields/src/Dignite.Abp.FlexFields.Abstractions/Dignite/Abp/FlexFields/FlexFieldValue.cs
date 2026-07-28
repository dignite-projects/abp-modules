using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// One flex field as it exists at runtime for one host entity: its <see cref="IFlexFieldData"/> definition,
/// the per-usage flags the host resolved for it, and its current value read out of the host's value bag.
/// <para>
/// This is a data carrier to <i>instantiate</i>, not to inherit - the same relationship ABP's
/// <c>UserData</c> has to <c>IUserData</c>. A host produces these (see
/// <c>IFlexFieldProvider&lt;TEntity&gt;</c> in <c>Dignite.Abp.FlexFields.Domain</c>, converting its own
/// <c>IFlexField</c> entities via <c>ToFlexFieldData()</c>) and <see cref="IFieldType"/> consumes them.
/// </para>
/// <para>
/// <see cref="Field"/> is typed as the interface rather than the concrete <see cref="FlexFieldData"/> so
/// that a future distributed-event carrier (e.g. <c>FlexFieldEto : IFlexFieldData</c>) can be passed
/// straight in with no adapter - the same trick ABP's <c>UserEto : IUserData</c> relies on.
/// </para>
/// </summary>
public class FlexFieldValue
{
    public IFlexFieldData Field { get; }

    /// <summary>
    /// Whether a value is required <i>for this usage</i>. Per-usage rather than per-definition because
    /// a host commonly attaches one field definition to several host types with different rules.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Whether this usage's value should be decomposed into the host's query index
    /// (<see cref="IFieldType.GetSearchableValues"/>).
    /// </summary>
    public bool Searchable { get; }

    /// <summary>
    /// Current value, read from the host's <see cref="IHasFlexFields.FlexFields"/> bag. May be a
    /// <see cref="System.Text.Json.JsonElement"/> after a JSON round trip - field types handle that.
    /// </summary>
    public object? Value { get; }

    public FlexFieldValue(IFlexFieldData field, bool required = false, bool searchable = false, object? value = null)
    {
        Field = field;
        Required = required;
        Searchable = searchable;
        Value = value;
    }

    public Guid FieldId => Field.Id;

    public string Name => Field.Name;

    public string DisplayName => Field.DisplayName;

    public string FieldTypeName => Field.FieldTypeName;

    public FieldConfigurationDictionary Configuration => Field.Configuration;
}
