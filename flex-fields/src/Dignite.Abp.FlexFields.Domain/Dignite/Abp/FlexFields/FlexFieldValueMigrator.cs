using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Provider-agnostic default <see cref="IFlexFieldValueMigrator{TEntity}"/>: pages through every host
/// entity via <see cref="IFlexFieldProvider{TEntity}"/> and rewrites its value bag.
/// <para>
/// There is deliberately no EF or Mongo variant of this - moving a key inside the bag is the same operation
/// whatever stores it, and both halves that could have been provider-specific already have provider-neutral
/// seams: enumeration through the provider, persistence through ABP's repository. Contrast
/// <see cref="IFlexFieldIndexManager{TEntity}"/>, whose implementations differ because the derived state
/// itself differs in kind.
/// </para>
/// <para>
/// Registered as an open generic by <see cref="FlexFieldsDomainModule"/>, so it serves every host entity
/// type without per-type registration. Members are virtual, and a host that needs different behaviour can
/// subclass or replace it.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type.</typeparam>
public class FlexFieldValueMigrator<TEntity> : IFlexFieldValueMigrator<TEntity>
    where TEntity : class, IHasFlexFields, IEntity
{
    protected IFlexFieldProvider<TEntity> FlexFieldProvider { get; }

    protected IBasicRepository<TEntity> Repository { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// How many host entities are processed per flush. Bounds memory on a migration; override to trade
    /// round trips against change-tracker size.
    /// </summary>
    protected virtual int MigrationPageSize => 100;

    public FlexFieldValueMigrator(
        IFlexFieldProvider<TEntity> flexFieldProvider,
        IBasicRepository<TEntity> repository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        FlexFieldProvider = flexFieldProvider;
        Repository = repository;
        UnitOfWorkManager = unitOfWorkManager;
    }

    public virtual Task<int> RenameFieldAsync(
        string oldName,
        string newName,
        CancellationToken cancellationToken = default)
    {
        return MigrateAsync(entity => entity.RenameField(oldName, newName), cancellationToken);
    }

    public virtual Task<int> RemoveFieldAsync(string name, CancellationToken cancellationToken = default)
    {
        return MigrateAsync(
            entity =>
            {
                if (!entity.HasField(name))
                {
                    return false;
                }

                entity.RemoveField(name);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Walks every host entity through <see cref="IFlexFieldProvider{TEntity}.GetPagedEntitiesAsync"/> and
    /// applies <paramref name="mutate"/>, which reports whether it changed that entity's bag. Flushes per
    /// page rather than at the end, because the point of paging is to keep memory bounded.
    /// <para>
    /// Changed entities go through <c>UpdateAsync</c> rather than relying on change tracking, because only
    /// EF has any: a document provider needs the explicit write. <c>autoSave: false</c> keeps that from
    /// becoming one flush per entity - the page boundary is where the unit of work is told to save.
    /// </para>
    /// <para>
    /// Every host entity is visited, including the ones that never held the key - the kernel has no way to
    /// push a bag-key predicate down through the provider's own query. This is the same cost model as
    /// <see cref="IFlexFieldIndexManager{TEntity}.RebuildAsync"/>.
    /// </para>
    /// <para>
    /// Flushing per page also means this is not atomic: a failure part way through leaves the earlier pages
    /// migrated. Both operations are idempotent, so re-running after fixing the cause converges.
    /// </para>
    /// </summary>
    protected virtual async Task<int> MigrateAsync(
        Func<TEntity, bool> mutate,
        CancellationToken cancellationToken)
    {
        var skipCount = 0;
        var changedCount = 0;

        while (true)
        {
            var page = await FlexFieldProvider.GetPagedEntitiesAsync(skipCount, MigrationPageSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            var pageChangedCount = 0;
            foreach (var entity in page)
            {
                if (!mutate(entity))
                {
                    continue;
                }

                await Repository.UpdateAsync(entity, autoSave: false, cancellationToken: cancellationToken);
                pageChangedCount++;
            }

            if (pageChangedCount > 0)
            {
                await SaveChangesAsync(cancellationToken);
                changedCount += pageChangedCount;
            }

            if (page.Count < MigrationPageSize)
            {
                break;
            }

            skipCount += MigrationPageSize;
        }

        return changedCount;
    }

    /// <summary>
    /// Flushes one page. Null-safe on the ambient unit of work only because a provider whose repository
    /// already wrote through has nothing left to flush - an EF host without a unit of work would have
    /// failed inside <c>UpdateAsync</c> long before reaching here.
    /// </summary>
    protected virtual Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return UnitOfWorkManager.Current?.SaveChangesAsync(cancellationToken) ?? Task.CompletedTask;
    }
}
