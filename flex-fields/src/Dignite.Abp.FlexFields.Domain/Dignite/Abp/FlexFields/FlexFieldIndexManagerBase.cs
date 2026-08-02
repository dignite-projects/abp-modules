using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Everything an <see cref="IFlexFieldIndexManager{TEntity}"/> does that is not about a particular store:
/// enumerating host entities a page at a time, and deciding which of a host's field values are eligible to be
/// indexed at all. A provider supplies only the two things that genuinely differ - what it does to one
/// entity, and how it flushes.
/// <para>
/// The eligibility rule lives here rather than in each provider because it is the answer to "what does
/// <c>Searchable</c> mean", which is the module's to give. Two providers disagreeing about it would make the
/// same host queryable by different fields depending on what stores it - and the disagreement would only ever
/// surface as a missing search result.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type, e.g. a CMS's <c>Entry</c>.</typeparam>
public abstract class FlexFieldIndexManagerBase<TEntity> : IFlexFieldIndexManager<TEntity>
    where TEntity : class, IHasFlexFields, IEntity<Guid>
{
    protected IFlexFieldProvider<TEntity> FlexFieldProvider { get; }

    protected IFieldTypeResolver FieldTypeResolver { get; }

    /// <summary>
    /// How many host entities <see cref="RebuildAsync"/> processes per flush. Bounds memory on a full
    /// rebuild; override to trade round trips against how much work is held before writing.
    /// </summary>
    protected virtual int RebuildPageSize => 100;

    protected FlexFieldIndexManagerBase(
        IFlexFieldProvider<TEntity> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver)
    {
        FlexFieldProvider = flexFieldProvider;
        FieldTypeResolver = fieldTypeResolver;
    }

    /// <summary>
    /// Deliberately does not flush: the derived state must commit with the value bag it came from, so it
    /// rides the caller's ambient unit of work.
    /// </summary>
    public virtual async Task SynchronizeAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await SynchronizeCoreAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Unlike <see cref="SynchronizeAsync"/> this does flush - per page, which is the point of paging.
    /// </summary>
    public virtual async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        await FlexFieldEntityPager.ForEachPageAsync(
            FlexFieldProvider,
            RebuildPageSize,
            SynchronizeCoreAsync,
            FlushAsync,
            cancellationToken);
    }

    /// <summary>
    /// The host's field values that are eligible to be indexed, each paired with its resolved field type.
    /// <para>
    /// Two filters, and this class applies both itself rather than trusting a field type to honour them.
    /// Only <see cref="FlexFieldValue.Searchable"/> usages are indexed - that is this manager's policy, not a
    /// field type's. And a field type with no <see cref="IFieldType.IndexValueType"/> (RichText, Matrix) is
    /// skipped explicitly rather than being left to yield nothing on its own, because there is no typed
    /// reading to pair its values with.
    /// </para>
    /// </summary>
    protected virtual async Task<IReadOnlyList<IndexableFlexField>> GetIndexableFieldsAsync(
        TEntity entity,
        CancellationToken cancellationToken)
    {
        var fields = await FlexFieldProvider.GetFlexFieldsAsync(entity, cancellationToken);
        var indexable = new List<IndexableFlexField>();

        foreach (var field in fields)
        {
            if (!field.Searchable)
            {
                continue;
            }

            var fieldType = FieldTypeResolver.Get(field.FieldTypeName);
            if (!fieldType.IsIndexable())
            {
                continue;
            }

            indexable.Add(new IndexableFlexField(field, fieldType));
        }

        return indexable;
    }

    /// <summary>
    /// Brings one entity's derived state in line with its bag, without flushing.
    /// </summary>
    /// <returns>
    /// Whether anything was actually written. A provider that always rewrites returns <c>true</c>; one that
    /// only writes when a value was not already in its indexed form returns whether it had to, which is what
    /// keeps a rebuild that changes nothing from costing a write per page.
    /// </returns>
    protected abstract Task<bool> SynchronizeCoreAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Commits one page's worth of <see cref="SynchronizeCoreAsync"/> calls.
    /// </summary>
    protected abstract Task FlushAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A host field value that is eligible to be indexed, with the field type already resolved so a caller does
/// not resolve it again. <see cref="IndexValueType"/> is non-null by construction - eligibility is what
/// established that.
/// </summary>
public readonly record struct IndexableFlexField(FlexFieldValue Value, IFieldType FieldType)
{
    public FlexFieldValueType IndexValueType => FieldType.IndexValueType!.Value;
}
