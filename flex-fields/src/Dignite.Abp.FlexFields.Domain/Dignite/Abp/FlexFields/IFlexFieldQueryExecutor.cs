using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Narrows a host query to entities matching field conditions. Genuinely provider-agnostic - a relational
/// provider composes its typed pivot table as a subquery, a document store queries the embedded bag with
/// native path indexes - and always pushed down to the database, never a filter applied to
/// already-materialized entities. The host stays in charge of its own query: this only adds predicates to
/// it, so paging, sorting, and further host-side filtering compose on top of the result untouched.
/// </summary>
/// <typeparam name="TEntity">The host entity type being filtered, e.g. a CMS's <c>Entry</c>.</typeparam>
public interface IFlexFieldQueryExecutor<TEntity>
    where TEntity : class, IHasFlexFields, IEntity<Guid>
{
    /// <summary>
    /// Returns <paramref name="query"/> narrowed to entities matching ALL of <paramref name="conditions"/>.
    /// An empty condition list is rejected rather than treated as "match everything" - that is the host's
    /// own unfiltered query, not a flex field query.
    /// </summary>
    Task<IQueryable<TEntity>> ApplyFilterAsync(
        IQueryable<TEntity> query,
        IReadOnlyList<FlexFieldQueryCondition> conditions,
        CancellationToken cancellationToken = default);
}
