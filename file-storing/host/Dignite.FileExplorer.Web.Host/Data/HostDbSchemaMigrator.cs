using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.FileExplorer.Web.Host.Data;

public class HostDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public HostDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolve the HostDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
        */
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
