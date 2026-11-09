using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// Runs <see cref="ClientIdMetadataDocumentCleanupService"/> every
/// <see cref="AbpClientIdMetadataDocumentOptions.CleanupPeriod"/>, on one instance at a time - the same shape as ABP's
/// own TokenCleanupBackgroundWorker.
/// </summary>
internal class ClientIdMetadataDocumentCleanupBackgroundWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IAbpDistributedLock _distributedLock;

    public ClientIdMetadataDocumentCleanupBackgroundWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AbpClientIdMetadataDocumentOptions> options,
        IAbpDistributedLock distributedLock)
        : base(timer, serviceScopeFactory)
    {
        _distributedLock = distributedLock;
        timer.Period = (int)Math.Clamp(options.Value.CleanupPeriod.TotalMilliseconds, 1_000, int.MaxValue);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(nameof(ClientIdMetadataDocumentCleanupBackgroundWorker));
        if (handle == null)
        {
            return;
        }

        await workerContext.ServiceProvider
            .GetRequiredService<ClientIdMetadataDocumentCleanupService>()
            .CleanAsync(workerContext.CancellationToken);
    }
}
