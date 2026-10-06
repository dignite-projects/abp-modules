using System;
using System.Collections.Generic;
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
/// asks, so the registry's tenant filter scopes the lookup. Override <see cref="IsActiveAsync"/> to stop pushing to
/// devices by a rule of the host's — <c>Dignite.NotificationCenter.Push.Identity</c> does it for ended login sessions.
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
        var targets = new List<PushTarget>();
        foreach (var device in await PushDeviceManager.GetListAsync(userId, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await IsActiveAsync(device, cancellationToken))
            {
                await PushDeviceManager.RemoveAsync(device, cancellationToken);
                continue;
            }

            targets.Add(new PushTarget(device.Provider, device.Token, device.CultureName));
        }

        return targets;
    }

    public virtual Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
    {
        return PushDeviceManager.RemoveAsync(provider, token, cancellationToken);
    }

    /// <summary>
    /// Whether the device should still be pushed to. A device found inactive is forgotten, not just skipped.
    /// </summary>
    protected virtual Task<bool> IsActiveAsync(PushDevice device, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
