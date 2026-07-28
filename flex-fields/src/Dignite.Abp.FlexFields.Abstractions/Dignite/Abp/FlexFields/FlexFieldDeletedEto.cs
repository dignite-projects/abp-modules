using System;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Announces that a field definition is gone, so its key should be dropped from every host entity's value
/// bag. A subscriber's job is to call <c>IFlexFieldValueMigrator&lt;TEntity&gt;.RemoveFieldAsync(Name)</c>
/// for each host type the field was attached to; left alone, the values stay in the bag forever -
/// unreachable, and still serialized on every read.
/// <para>
/// <b>The kernel neither publishes nor handles this</b>, for the same reasons as
/// <see cref="FlexFieldRenamedEto"/>: only the downstream owns the definition entity, and only the
/// downstream knows which host types to fan out to.
/// </para>
/// <para>
/// <b><see cref="Name"/> has to be captured before the definition is deleted.</b> Unlike a rename, a
/// subscriber cannot look it up from <see cref="FieldId"/> afterwards - the row is gone. This is why the
/// name travels on the event rather than being resolved by the handler.
/// </para>
/// <para>
/// Timing and error handling carry the same caveats as <see cref="FlexFieldRenamedEto"/>, with one
/// difference in severity: a delayed cleanup only leaves dead weight in the bag, where a delayed rename
/// actively loses index rows. Redelivery is safe - <c>RemoveFieldAsync</c> is idempotent.
/// </para>
/// </summary>
[EventName("Dignite.Abp.FlexFields.FlexFieldDeleted")]
public class FlexFieldDeletedEto : IMultiTenant
{
    /// <summary>
    /// Tenant the definition belonged to, or null for a host-level one. Carried so the event bus can restore
    /// the right tenant context before the handler runs.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Identity of the deleted definition - for correlating with a subscriber's own records of which host
    /// types it was attached to. It no longer resolves to a definition row.
    /// </summary>
    public Guid FieldId { get; set; }

    /// <summary>
    /// The key to drop from the value bags, as the definition's <c>Name</c> read before deletion.
    /// </summary>
    public string Name { get; set; } = default!;

    public FlexFieldDeletedEto()
    {
    }

    public FlexFieldDeletedEto(Guid fieldId, string name, Guid? tenantId = null)
    {
        FieldId = fieldId;
        Name = name;
        TenantId = tenantId;
    }
}
