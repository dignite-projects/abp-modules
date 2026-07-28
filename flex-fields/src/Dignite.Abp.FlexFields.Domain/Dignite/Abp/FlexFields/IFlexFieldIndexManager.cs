using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Keeps whatever derived, queryable state a persistence provider maintains for a host entity type in
/// step with the authoritative value bags. Provider-agnostic on purpose: a relational provider rebuilds
/// a typed pivot table here, while a document store that indexes the embedded bag in place has nothing
/// to do and implements this as a no-op.
/// </summary>
/// <typeparam name="TEntity">The host entity type, e.g. a CMS's <c>Entry</c>.</typeparam>
public interface IFlexFieldIndexManager<in TEntity>
    where TEntity : class, IHasFlexFields, IEntity<Guid>
{
    /// <summary>
    /// Call after saving <paramref name="entity"/>, inside the same unit of work, so the derived state
    /// commits atomically with the value bag it was derived from.
    /// </summary>
    Task SynchronizeAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-derives the state for every current <typeparamref name="TEntity"/>, paging through them via
    /// <see cref="IFlexFieldProvider{TEntity}.GetPagedEntitiesAsync"/>.
    /// <para>
    /// This is the answer to both reindex triggers: a field's Searchable flag being switched on, and a
    /// field's type changing so its values belong in a different slot -
    /// <see cref="IFieldType.GetSearchableValues"/> converts from the raw bag value on every run, so for
    /// those two, re-deriving is a complete migration.
    /// </para>
    /// <para>
    /// It is <b>not</b> an answer to a field being renamed. Both triggers above re-read the bag under the
    /// same key; a rename changes the key itself, so re-deriving faithfully finds nothing and deletes the
    /// rows that were there. Rewrite the bags first with
    /// <see cref="IFlexFieldValueMigrator{TEntity}.RenameFieldAsync"/> - after which the index needs no
    /// rebuild at all, since its rows key on field id and value.
    /// </para>
    /// </summary>
    Task RebuildAsync(CancellationToken cancellationToken = default);
}
