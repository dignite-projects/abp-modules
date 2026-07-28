using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// The one seam a downstream host implements: resolving which flex fields a given host entity has, and
/// with what values. The host merges its own field definitions (<see cref="IFlexField"/>, converted to
/// <see cref="FlexFieldData"/> via <c>ToFlexFieldData()</c>), its own per-usage Required/Searchable rules,
/// and the values in the entity's <see cref="IHasFlexFields.FlexFields"/> bag into
/// <see cref="FlexFieldValue"/> instances.
/// <para>
/// The kernel deliberately owns no field/host model of its own, so this is the only way it can learn
/// what fields an entity has - the formalization of what a CMS's <c>GetCustomizeFields()</c> does today.
/// This is the kernel's <b>host-entity axis</b>, and the kernel itself is the only caller.
/// </para>
/// <para>
/// Contrast <see cref="IFlexFieldRepository{TField}"/>, which walks the <b>field-definition axis</b> and
/// is a convenience for the downstream that the kernel never calls: per-usage Required/Searchable is not
/// on <see cref="IFlexField"/> at all (the same definition can be attached to several host types with
/// different rules, so those flags live on <see cref="FlexFieldValue"/> instead), and a field repository
/// has no access to any host entity's value bag. Enumeration of host entities also lives here rather than
/// on a repository, because rebuilding derived state needs to walk every host entity, and only the host
/// has a repository for its own type.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type, e.g. a CMS's <c>Entry</c> or a shop's <c>Product</c>.</typeparam>
public interface IFlexFieldProvider<TEntity>
    where TEntity : IHasFlexFields
{
    Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages through every current instance of <typeparamref name="TEntity"/>, for
    /// <see cref="IFlexFieldIndexManager{TEntity}.RebuildAsync"/>. An empty result ends the sequence, so
    /// the ordering must be stable across calls.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetPagedEntitiesAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
