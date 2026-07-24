using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;

namespace Dignite.Abp.Notifications;

/// <summary>Records enqueued job args instead of running them, so tests can assert the background path.</summary>
public class FakeBackgroundJobManager : IBackgroundJobManager
{
    public ConcurrentQueue<object> EnqueuedArgs { get; } = new();

    public Exception? EnqueueException { get; set; }

    public Task<string> EnqueueAsync<TArgs>(
        TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
    {
        EnqueuedArgs.Enqueue(args!);
        if (EnqueueException != null)
        {
            return Task.FromException<string>(EnqueueException);
        }

        return Task.FromResult(Guid.NewGuid().ToString());
    }
}
