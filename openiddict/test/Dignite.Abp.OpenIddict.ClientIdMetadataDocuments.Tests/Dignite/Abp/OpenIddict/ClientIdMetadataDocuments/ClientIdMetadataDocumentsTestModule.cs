using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.EntityFrameworkCore;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// An ABP authorization server as a host has one: OpenIddict with its EF Core store (SQLite in memory), a registered
/// MCP resource as cloud and site register theirs, and multi-tenancy on. Documents come from
/// <see cref="StubClientIdMetadataDocumentFetcher"/> instead of the network.
/// </summary>
[DependsOn(
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpOpenIddictClientIdMetadataDocumentsModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpAutofacModule)
)]
public class ClientIdMetadataDocumentsTestModule : AbpModule
{
    public const string Resource = "https://mcp.example.com/mcp";
    public const string ApiScope = "TestApi";

    private SqliteConnection? _sqliteConnection;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpOpenIddictAspNetCoreOptions>(options => options.AddDevelopmentEncryptionAndSigningCertificate = false);

        PreConfigure<OpenIddictServerBuilder>(builder =>
        {
            builder.AddEphemeralEncryptionKey();
            builder.AddEphemeralSigningKey();
            builder.UseAspNetCore().DisableTransportSecurityRequirement();
            builder.RegisterScopes(ApiScope);
            // Both slash forms, as cloud registers its MCP resources: OpenIddict's ValidateResources compares the raw
            // `resource` parameter with each registered Uri, so the server must know both.
            builder.RegisterResources(new Uri(Resource, UriKind.Absolute), new Uri(Resource + "/", UriKind.Absolute));
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpClientIdMetadataDocumentOptions>(options =>
        {
            options.Scopes.Add(ApiScope);
            options.Scopes.Add("profile");
        });

        Configure<AbpMultiTenancyOptions>(options => options.IsEnabled = true);

        // The cleanup worker is run by hand, not on a timer.
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);

        // ABP's authorization endpoint, reached once OpenIddict has validated a request, lives in a referenced
        // assembly; it looks for the Identity cookie and asks an anonymous user to sign in.
        context.Services.GetSingletonInstance<ApplicationPartManager>()
            .ApplicationParts.AddIfNotContains(typeof(AbpOpenIddictAspNetCoreModule).Assembly);
        context.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);

        context.Services.AddSingleton<StubClientIdMetadataDocumentFetcher>();
        context.Services.Replace(ServiceDescriptor.Transient<ClientIdMetadataDocumentFetcher>(
            serviceProvider => serviceProvider.GetRequiredService<StubClientIdMetadataDocumentFetcher>()));

        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        _sqliteConnection.Open();
        using (var dbContext = new OpenIddictDbContext(
                   new DbContextOptionsBuilder<OpenIddictDbContext>().UseSqlite(_sqliteConnection).Options))
        {
            dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext => configurationContext.DbContextOptions.UseSqlite(_sqliteConnection));
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseAuthentication();
        app.UseUnitOfWork();
        app.UseAuthorization();
        app.UseConfiguredEndpoints();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }
}
