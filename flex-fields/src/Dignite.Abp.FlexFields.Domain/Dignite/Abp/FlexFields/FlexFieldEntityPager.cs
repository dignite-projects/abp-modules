using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Walks every host entity of a type through <see cref="IFlexFieldProvider{TEntity}.GetPagedEntitiesAsync"/>,
/// flushing once per page.
/// <para>
/// Both whole-collection operations the kernel performs - rebuilding derived state
/// (<see cref="IFlexFieldIndexManager{TEntity}.RebuildAsync"/>) and rewriting bag keys
/// (<see cref="IFlexFieldValueMigrator{TEntity}"/>) - are this same walk with a different body, so the walk
/// itself lives here rather than being written out at each site. The termination conditions in particular are
/// the kind of thing that is easy to get subtly different in two places: a short page ends the walk, and so
/// does an empty one, which is what makes an exact multiple of the page size terminate rather than spin.
/// </para>
/// <para>
/// Flushing per page is what bounds memory, and it is also why neither operation is atomic: a failure part
/// way through leaves the earlier pages written. Both callers are idempotent, so re-running after fixing the
/// cause converges.
/// </para>
/// <para>
/// Every host entity is visited, including ones the operation will not touch - the kernel has no way to push
/// a bag predicate down through a downstream's own query.
/// </para>
/// </summary>
public static class FlexFieldEntityPager
{
    /// <summary>
    /// Applies <paramref name="process"/> to every entity, calling <paramref name="flushPage"/> at the end of
    /// any page it reported a change on, and returns how many entities it changed.
    /// </summary>
    /// <param name="process">
    /// Handles one entity and reports whether it changed anything. Returning <c>false</c> for a whole page
    /// skips that page's flush entirely, so an operation that is already a no-op costs only its reads.
    /// </param>
    public static async Task<int> ForEachPageAsync<TEntity>(
        IFlexFieldProvider<TEntity> provider,
        int pageSize,
        Func<TEntity, CancellationToken, Task<bool>> process,
        Func<CancellationToken, Task> flushPage,
        CancellationToken cancellationToken = default)
        where TEntity : IHasFlexFields
    {
        var skipCount = 0;
        var changedCount = 0;

        while (true)
        {
            var page = await provider.GetPagedEntitiesAsync(skipCount, pageSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            var pageChangedCount = 0;
            foreach (var entity in page)
            {
                if (await process(entity, cancellationToken))
                {
                    pageChangedCount++;
                }
            }

            if (pageChangedCount > 0)
            {
                await flushPage(cancellationToken);
                changedCount += pageChangedCount;
            }

            if (page.Count < pageSize)
            {
                break;
            }

            skipCount += pageSize;
        }

        return changedCount;
    }
}
