using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// The store in place until something real replaces it: no user has a device, so the push channel delivers nothing.
/// Warns once per process, because a push channel with no device source is almost always a missing package.
/// </summary>
public class NullPushDeviceStore : IPushDeviceStore, ISingletonDependency
{
    private int _warned;

    protected ILogger<NullPushDeviceStore> Logger { get; }

    public NullPushDeviceStore(ILogger<NullPushDeviceStore> logger)
    {
        Logger = logger;
    }

    public virtual Task<IReadOnlyList<PushTarget>> GetTargetsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            Logger.LogWarning(
                "Notification push is installed but no {Store} is registered, so no push notifications will be sent. "
                + "Install Dignite.NotificationCenter.Push or register your own store.",
                nameof(IPushDeviceStore));
        }

        return Task.FromResult<IReadOnlyList<PushTarget>>(Array.Empty<PushTarget>());
    }

    public virtual Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
