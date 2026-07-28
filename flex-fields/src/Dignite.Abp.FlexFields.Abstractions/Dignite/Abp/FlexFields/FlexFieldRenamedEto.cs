using System;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Announces that a field definition's <c>Name</c> - and therefore the key its values are stored under in
/// every host entity's value bag - has changed. A subscriber's job is to call
/// <c>IFlexFieldValueMigrator&lt;TEntity&gt;.RenameFieldAsync(OldName, NewName)</c> for each host type the
/// field is attached to.
/// <para>
/// <b>The kernel neither publishes nor handles this.</b> Publishing is the downstream's because
/// <c>IFlexField.Name</c> is a get-only contract on an entity the downstream owns - the kernel cannot
/// observe a rename. Handling is the downstream's because the migrator is generic over the host entity type
/// and the kernel holds no registry of which host types a field is attached to. This type exists for the
/// case where those two are not the same downstream - a field-definition module that legitimately cannot
/// know its hosts. When they are the same, calling the migrator directly is simpler and safer; see the
/// caveats below.
/// </para>
/// <para>
/// <b>Timing.</b> <c>IEventBus.PublishAsync</c> defaults to <c>onUnitOfWorkComplete: true</c>, so by default
/// the handler runs only after the unit of work that changed the name has committed. Anything that saves a
/// host entity in that window calls <c>IFlexFieldIndexManager.SynchronizeAsync</c>, projects nothing for the
/// field, and deletes its index rows - the exact failure the migrator exists to prevent, reintroduced as a
/// race. Publishing with <c>onUnitOfWorkComplete: false</c> closes it by running the migration in the same
/// unit of work, at the cost of a paged bulk migration inside the caller's transaction. A concurrency-safe
/// alternative that needs neither is to migrate in three idempotent steps - copy the value to the new key,
/// then flip <c>Name</c>, then drop the old key - so some key always matches the current name.
/// </para>
/// <para>
/// <b>Errors.</b> A handler's exception is collected by the event bus, not returned to the publisher. In
/// particular the collision guard in <c>FlexFieldDictionaryExtensions.RenameField</c> - which throws rather
/// than silently discarding one of two values - degrades to a log entry here. Rule the new name out with
/// <c>IFlexFieldRepository{TField}.NameExistsAsync(newName, excludedId)</c> before publishing.
/// </para>
/// <para>
/// Redelivery is safe: <c>RenameFieldAsync</c> is idempotent, so at-least-once delivery costs nothing.
/// </para>
/// </summary>
[EventName("Dignite.Abp.FlexFields.FlexFieldRenamed")]
public class FlexFieldRenamedEto : IMultiTenant
{
    /// <summary>
    /// Tenant the definition belongs to, or null for a host-level one. Carried so the event bus can restore
    /// the right tenant context before the handler runs - without it a handler that runs out of band (a
    /// deferred local event, or any distributed one) would migrate whatever tenant it happens to land in.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Identity of the renamed definition. Not needed to move the key, but it is how a subscriber looks the
    /// definition back up to decide which host types to migrate.
    /// </summary>
    public Guid FieldId { get; set; }

    /// <summary>The key host value bags still hold the value under.</summary>
    public string OldName { get; set; } = default!;

    /// <summary>The definition's new <c>Name</c>, and the key the value has to end up under.</summary>
    public string NewName { get; set; } = default!;

    public FlexFieldRenamedEto()
    {
    }

    public FlexFieldRenamedEto(Guid fieldId, string oldName, string newName, Guid? tenantId = null)
    {
        FieldId = fieldId;
        OldName = oldName;
        NewName = newName;
        TenantId = tenantId;
    }
}
