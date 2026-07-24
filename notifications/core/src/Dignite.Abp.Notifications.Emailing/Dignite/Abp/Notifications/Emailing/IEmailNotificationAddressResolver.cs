using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Supplies the email address and optional culture for one recipient of one notification. Resolvers form an ordered
/// chain and the first non-null result wins, so an application resolver at <see cref="NotificationEmailProviderOrders.Default"/> can
/// claim specific notifications while the built-in Identity resolver stays as the fallback.
/// </summary>
/// <remarks>
/// <para>
/// <b>A resolver returns the address <i>for this user</i> in this entity context — never "the entity's address".</b>
/// <see cref="EmailNotificationAddressResolveContext.UserId"/> is always one of the notification's recipients, and
/// <c>EmailNotifier</c> builds the body for that same user. A resolver that keys off
/// <c>context.Notification.EntityId</c> alone returns one address for every recipient, so N recipients get N copies
/// of a body written for someone else, and the entity's contact may not be a recipient at all.
/// </para>
/// <para>
/// Claim on <c>context.Notification.NotificationName</c> in preference to <c>EntityTypeName</c>: one order entity can
/// carry both <c>"Mall.OrderShipped"</c> (the account email) and <c>"Mall.OrderContactChanged"</c> (the order's
/// contact). <c>EntityTypeName</c> / <c>EntityId</c> are for looking the entity <i>up</i>, not for deciding ownership.
/// </para>
/// <para>
/// Returning null means "not mine, or no address for this user" — the chain continues, and the Identity fallback may
/// still mail the account address. There is deliberately no way to say "claim this notification and send nothing":
/// suppressing a channel for one recipient is consent, not address resolution, and it has no home yet. See #36.
/// </para>
/// <para>
/// <b>Tenancy</b>: never call <c>CurrentTenant.Change</c>. ABP's event bus has already entered the notification's
/// tenant before the notifier runs, so a repository-backed resolver queries under the ambient tenant.
/// <see cref="EmailNotificationAddressResolveContext.TenantId"/> is for resolvers that must forward the tenant across
/// a boundary the ambient scope cannot cross, such as a remote user service.
/// </para>
/// </remarks>
public interface IEmailNotificationAddressResolver
{
    /// <summary>Lower runs first. See <see cref="NotificationEmailProviderOrders"/>.</summary>
    int Order { get; }

    /// <summary>The address and optional culture to use, or null to pass the recipient to the next resolver.</summary>
    Task<EmailNotificationAddress?> GetEmailOrNullAsync(
        EmailNotificationAddressResolveContext context,
        CancellationToken cancellationToken = default);
}
