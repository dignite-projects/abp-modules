using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// Saves the static definitions and warms the dynamic store when the application starts, as ABP's
/// <c>PermissionDynamicInitializer</c>: in the background, saving with Polly retries (eight attempts, exponential
/// back-off with jitter), so a database or cache that is not reachable yet delays the save instead of failing the start.
/// </summary>
public class NotificationDynamicInitializer : ITransientDependency
{
    public ILogger<NotificationDynamicInitializer> Logger { get; set; }

    protected IServiceProvider ServiceProvider { get; }

    public NotificationDynamicInitializer(IServiceProvider serviceProvider)
    {
        Logger = NullLogger<NotificationDynamicInitializer>.Instance;
        ServiceProvider = serviceProvider;
    }

    public virtual Task InitializeAsync(bool runInBackground, CancellationToken cancellationToken = default)
    {
        var options = ServiceProvider.GetRequiredService<IOptions<NotificationDefinitionStoreOptions>>().Value;

        if (!options.SaveStaticNotificationsToDatabase && !options.IsDynamicNotificationStoreEnabled)
        {
            return Task.CompletedTask;
        }

        if (runInBackground)
        {
            var applicationLifetime = ServiceProvider.GetService<IHostApplicationLifetime>();
            Task.Run(async () =>
            {
                if (cancellationToken == default && applicationLifetime?.ApplicationStopping != null)
                {
                    cancellationToken = applicationLifetime.ApplicationStopping;
                }

                await ExecuteInitializationAsync(options, cancellationToken);
            }, cancellationToken);

            return Task.CompletedTask;
        }

        return ExecuteInitializationAsync(options, cancellationToken);
    }

    protected virtual async Task ExecuteInitializationAsync(
        NotificationDefinitionStoreOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var cancellationTokenProvider = ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
            using (cancellationTokenProvider.Use(cancellationToken))
            {
                if (cancellationTokenProvider.Token.IsCancellationRequested)
                {
                    return;
                }

                await SaveStaticNotificationsToDatabaseAsync(options, cancellationToken);

                if (cancellationTokenProvider.Token.IsCancellationRequested)
                {
                    return;
                }

                await PreCacheDynamicNotificationsAsync(options);
            }
        }
        catch
        {
            // No need to log here since inner calls log
        }
    }

    protected virtual async Task SaveStaticNotificationsToDatabaseAsync(
        NotificationDefinitionStoreOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.SaveStaticNotificationsToDatabase)
        {
            return;
        }

        var staticNotificationDefinitionSaver = ServiceProvider.GetRequiredService<IStaticNotificationDefinitionSaver>();

        await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                8,
                retryAttempt => TimeSpan.FromSeconds(
                    RandomHelper.GetRandom(
                        (int)Math.Pow(2, retryAttempt) * 8,
                        (int)Math.Pow(2, retryAttempt) * 12)
                )
            )
            .ExecuteAsync(async _ =>
            {
                try
                {
                    await staticNotificationDefinitionSaver.SaveAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    throw; // Polly will catch it
                }
            }, cancellationToken);
    }

    protected virtual async Task PreCacheDynamicNotificationsAsync(NotificationDefinitionStoreOptions options)
    {
        if (!options.IsDynamicNotificationStoreEnabled)
        {
            return;
        }

        var dynamicNotificationDefinitionStore = ServiceProvider.GetRequiredService<IDynamicNotificationDefinitionStore>();

        try
        {
            // Pre-cache notification definitions, so the first request doesn't wait
            await dynamicNotificationDefinitionStore.GetGroupsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            throw; // It will be caught in ExecuteInitializationAsync
        }
    }
}
