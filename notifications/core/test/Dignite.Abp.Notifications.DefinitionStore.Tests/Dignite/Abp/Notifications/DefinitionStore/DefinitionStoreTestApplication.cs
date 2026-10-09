using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// A started ABP application with its own name — one service of the deployment that
/// <see cref="SharedDefinitionStoreInfrastructure"/> describes.
/// </summary>
public sealed class DefinitionStoreTestApplication : IAsyncDisposable
{
    private readonly IAbpApplicationWithInternalServiceProvider _application;

    private DefinitionStoreTestApplication(IAbpApplicationWithInternalServiceProvider application)
    {
        _application = application;
    }

    public IServiceProvider ServiceProvider => _application.ServiceProvider;

    public static async Task<DefinitionStoreTestApplication> StartAsync<TStartupModule>(
        string applicationName,
        SharedDefinitionStoreInfrastructure shared,
        Action<IServiceCollection>? configureServices = null)
        where TStartupModule : IAbpModule
    {
        var application = await AbpApplicationFactory.CreateAsync<TStartupModule>(options =>
        {
            options.ApplicationName = applicationName;
            options.UseAutofac();
            options.Services.AddSingleton(shared);
            configureServices?.Invoke(options.Services);
        });

        await application.InitializeAsync();
        return new DefinitionStoreTestApplication(application);
    }

    public T Get<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>Saves this application's static definitions, as its startup does.</summary>
    public Task SaveStaticDefinitionsAsync()
    {
        return Get<IStaticNotificationDefinitionSaver>().SaveAsync();
    }

    /// <summary>The names in the definition table, read in a unit of work of their own.</summary>
    public Task<List<string>> GetStoredNotificationNamesAsync()
    {
        return WithUnitOfWorkAsync(async services =>
            (await services.GetRequiredService<INotificationDefinitionRecordRepository>().GetListAsync())
            .Select(record => record.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>The names in the group table, read in a unit of work of their own.</summary>
    public Task<List<string>> GetStoredGroupNamesAsync()
    {
        return WithUnitOfWorkAsync(async services =>
            (await services.GetRequiredService<INotificationGroupDefinitionRecordRepository>().GetListAsync())
            .Select(record => record.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList());
    }

    public async Task<T> WithUnitOfWorkAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = ServiceProvider.CreateScope();
        using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var result = await action(scope.ServiceProvider);
        await unitOfWork.CompleteAsync();
        return result;
    }

    public async Task WithUnitOfWorkAsync(Func<IServiceProvider, Task> action)
    {
        await WithUnitOfWorkAsync(async services =>
        {
            await action(services);
            return true;
        });
    }

    /// <summary>As if the dynamic store's 30-second window had passed since its last stamp check.</summary>
    public void ExpireDynamicStoreCheckWindow()
    {
        Get<IDynamicNotificationDefinitionStoreInMemoryCache>().LastCheckTime = DateTime.Now.AddSeconds(-31);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.ShutdownAsync();
        _application.Dispose();
    }
}
