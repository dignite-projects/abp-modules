using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// The devices a user can be pushed to, and the place to drop one a push provider has reported dead. Unlike an email
/// address, a device token has no natural home in a user store: the app reports it, it changes, and a user has several.
/// </summary>
/// <remarks>
/// <para>
/// With the Notification Center installed, <c>Dignite.NotificationCenter.Push</c> implements this over its
/// <c>PushDevice</c> registry. A stateless host (no Notification Center) implements it against its own device storage. Without
/// either, <see cref="NullPushDeviceStore"/> answers "no devices" and the push channel delivers nothing.
/// </para>
/// <para>
/// <b>Tenancy</b>: never call <c>CurrentTenant.Change</c>. ABP's event bus has already entered the notification's
/// tenant before the notifier runs, so a repository-backed store queries under the ambient tenant.
/// </para>
/// </remarks>
public interface IPushDeviceStore
{
    /// <summary>The user's registered devices. Empty when the user has none.</summary>
    Task<IReadOnlyList<PushTarget>> GetTargetsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets a device a push provider reported as no longer registered (the app was uninstalled, the token rotated).
    /// Removing an unknown device is not an error.
    /// </summary>
    Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default);
}
