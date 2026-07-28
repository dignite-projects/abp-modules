using System;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Base for a downstream-owned query-index row: the derived, typed projection of one value of one field
/// of one host entity instance. The authoritative value always stays in the host's own
/// <see cref="IHasFlexFields.FlexFields"/> bag; these rows exist only so "find hosts where field X
/// matches Y" can run as SQL instead of in memory.
/// <para>
/// One index table per host entity type - <typeparamref name="TEntity"/> is the type marker that keeps
/// them distinct, so no <c>EntityType</c> discriminator column is needed. The kernel maps no
/// relationship: the downstream declares its own foreign key to the host, its own table name, and its
/// own indexes, then calls
/// <see cref="FlexFieldsDbContextModelCreatingExtensions.ConfigureFlexFieldIndex{TIndex}"/> for the
/// value columns:
/// </para>
/// <code>
/// public class EntryFlexFieldIndex : FlexFieldIndexBase&lt;Entry&gt;
/// {
///     public virtual Guid EntryId { get; set; }
/// }
/// </code>
/// <para>
/// An <see cref="Entity{TKey}"/> rather than an aggregate root: an index row is a child row of its host,
/// never independently loaded.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type these rows index, e.g. a CMS's <c>Entry</c>.</typeparam>
public abstract class FlexFieldIndexBase<TEntity> : Entity<Guid>, IFlexFieldIndex
    where TEntity : class, IHasFlexFields
{
    /// <summary>
    /// The <c>IFlexField.Id</c> of the field definition this row projects.
    /// </summary>
    public virtual Guid FieldId { get; set; }

    /// <summary>
    /// Which of the value columns below carries this row's value.
    /// </summary>
    public virtual FlexFieldValueType ValueType { get; set; }

    public virtual string? StringValue { get; set; }

    public virtual decimal? NumberValue { get; set; }

    public virtual DateTime? DateTimeValue { get; set; }

    public virtual bool? BooleanValue { get; set; }

    public virtual Guid? GuidValue { get; set; }

    protected FlexFieldIndexBase()
    {
    }

    protected FlexFieldIndexBase(Guid id)
        : base(id)
    {
    }

    /// <summary>
    /// Copies a value already typed into a slot (<see cref="FlexFieldIndexValue.Create"/>) into this
    /// row's typed columns.
    /// </summary>
    public virtual void SetValue(Guid fieldId, FlexFieldIndexValue value)
    {
        FieldId = fieldId;
        ValueType = value.ValueType;
        StringValue = value.StringValue;
        NumberValue = value.NumberValue;
        DateTimeValue = value.DateTimeValue;
        BooleanValue = value.BooleanValue;
        GuidValue = value.GuidValue;
    }
}
