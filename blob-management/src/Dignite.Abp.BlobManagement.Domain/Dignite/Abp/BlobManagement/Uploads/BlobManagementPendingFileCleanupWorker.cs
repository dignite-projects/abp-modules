using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// Runs <see cref="BlobManagementPendingFileCleaner"/> every <see cref="BlobManagementUploadOptions.CleanupPeriod"/>.
/// A host that turns ABP's background workers off (<c>AbpBackgroundWorkerOptions.IsEnabled</c>) runs the cleaner itself.
/// </summary>
public class BlobManagementPendingFileCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
    public BlobManagementPendingFileCleanupWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BlobManagementUploadOptions> options)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = (int)options.Value.CleanupPeriod.TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await workerContext.ServiceProvider
            .GetRequiredService<BlobManagementPendingFileCleaner>()
            .CleanAsync(workerContext.CancellationToken);
    }
}
