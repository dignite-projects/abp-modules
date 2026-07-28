using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Rewrites the keys of the authoritative value bags when a field definition's identity on the bag
/// changes. A definition's <see cref="IFlexField.Name"/> <i>is</i> the key its values are stored under, so
/// renaming or deleting a definition leaves every host entity holding a key nothing can reach - and
/// <see cref="IFlexFieldIndexManager{TEntity}.RebuildAsync"/> cannot repair it, because re-deriving reads
/// the bag by that same key.
/// <para>
/// Unlike <see cref="IFlexFieldIndexManager{TEntity}"/>, this has <b>one</b> implementation for every
/// provider - <see cref="FlexFieldValueMigrator{TEntity}"/> in <c>.Domain</c>, registered as an open
/// generic. The index manager needs a provider-specific implementation because the derived state it
/// maintains differs in kind (EF's typed pivot table versus Mongo's native path indexes); moving a key
/// inside the bag does not, and paging plus persistence are already provider-agnostic through
/// <see cref="IFlexFieldProvider{TEntity}"/> and ABP's repository. A downstream writes no code for this
/// beyond resolving it.
/// </para>
/// <para>
/// The kernel never calls it. <see cref="IFlexField.Name"/> is a get-only contract and the definition
/// entity belongs to the downstream, so the kernel cannot observe a rename, let alone trigger one - this is
/// the tool a downstream calls after changing its own entity, in this order:
/// </para>
/// <list type="number">
/// <item>rule the new name out as a duplicate with
/// <see cref="IFlexFieldRepository{TField}.NameExistsAsync"/>, passing the field's own id as
/// <c>excludedId</c>;</item>
/// <item>change <see cref="IFlexField.Name"/> on the downstream's own definition entity;</item>
/// <item>call <see cref="RenameFieldAsync"/> here, once per host type the field is attached to;</item>
/// <item>only then let anything call <see cref="IFlexFieldIndexManager{TEntity}.SynchronizeAsync"/> again.</item>
/// </list>
/// <para>
/// That last step is the trap: an entity synchronized between steps 2 and 3 projects nothing for the field
/// and has its existing index rows deleted. Nothing here reindexes, and nothing needs to for a rename -
/// index rows key on field id and value, neither of which a rename touches.
/// </para>
/// <para>
/// One host type per instance, the same shape as <see cref="IFlexFieldIndexManager{TEntity}"/>. A definition
/// can be attached to several host types (its per-usage Required/Searchable flags differ per host), so a
/// single rename means one call per host type - the downstream knows that list, the kernel does not.
/// </para>
/// <para>
/// That last point is also why the kernel ships no event handler. <see cref="FlexFieldRenamedEto"/> and
/// <see cref="FlexFieldDeletedEto"/> exist for a downstream whose field definitions and host entities live
/// in different modules, but both ends stay the downstream's: it publishes (the kernel cannot see a rename)
/// and it subscribes (the kernel cannot know the host types to fan out to). Read those types before wiring
/// them up - going through the event bus reopens a timing window and quietens the collision guard, so
/// calling this interface directly is the better default whenever the caller knows its host types.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type, e.g. a CMS's <c>Entry</c>.</typeparam>
public interface IFlexFieldValueMigrator<TEntity>
    where TEntity : class, IHasFlexFields, IEntity
{
    /// <summary>
    /// Moves every host entity's value from <paramref name="oldName"/> to <paramref name="newName"/>,
    /// returning how many entities actually changed. Entities without the old key are left alone, so this
    /// is idempotent and safe to re-run.
    /// </summary>
    Task<int> RenameFieldAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops <paramref name="name"/> from every host entity's bag, returning how many entities actually
    /// changed. For a deleted definition, whose values would otherwise sit in the bag forever, unreachable
    /// and still serialized on every read.
    /// </summary>
    Task<int> RemoveFieldAsync(string name, CancellationToken cancellationToken = default);
}
