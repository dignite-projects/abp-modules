using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Volo.Abp.DependencyInjection;

namespace Dignite.NotificationCenter.Push;

/// <summary>
/// Serves the push channel from the Notification Center's <see cref="PushDevice"/> registry, replacing the null store.
/// </summary>
/// <remarks>
/// Runs under the ambient tenant: ABP's event bus has already entered the notification's tenant before the notifier
/// asks, so the registry's tenant filter scopes the lookup. Override <see cref="FindInactiveAsync"/> to stop pushing
/// to devices by a rule of the host's — <c>Dignite.NotificationCenter.Push.Identity</c> does it for ended login
/// sessions.
/// </remarks>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPushDeviceStore), typeof(NotificationCenterPushDeviceStore))]
public class NotificationCenterPushDeviceStore : IPushDeviceStore, ITransientDependency
{
    protected PushDeviceManager PushDeviceManager { get; }

    public NotificationCenterPushDeviceStore(PushDeviceManager pushDeviceManager)
    {
        PushDeviceManager = pushDeviceManager;
    }

    public virtual async Task<IReadOnlyList<PushTarget>> GetTargetsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var devices = await PushDeviceManager.GetListAsync(userId, cancellationToken);
        if (devices.Count == 0)
        {
            return Array.Empty<PushTarget>();
        }

        var inactive = await FindInactiveAsync(userId, devices, cancellationToken);
        foreach (var device in inactive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PushDeviceManager.RemoveAsync(device, cancellationToken);
        }

        return devices
            .Except(inactive)
            .Select(device => new PushTarget(device.Provider, device.Token, device.CultureName))
            .ToList();
    }

    public virtual Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
    {
        return PushDeviceManager.RemoveAsync(provider, token, cancellationToken);
    }

    /// <summary>
    /// The devices of <paramref name="userId"/> that should no longer be pushed to; they are forgotten, not just
    /// skipped. Gets all of the user's devices at once so an override can judge them with one query.
    /// </summary>
    protected virtual Task<IReadOnlyCollection<PushDevice>> FindInactiveAsync(
        Guid userId,
        IReadOnlyList<PushDevice> devices,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<PushDevice>>(Array.Empty<PushDevice>());
    }
}
