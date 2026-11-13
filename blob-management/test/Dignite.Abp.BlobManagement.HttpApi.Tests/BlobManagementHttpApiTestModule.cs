using System;
using Dignite.Abp.BlobStoring.BlobManagement;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.EntityFrameworkCore;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Http.Client;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.MultiTenancy.ConfigurationStore;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// One test host playing both sides: Blob Management as its own service (Application, HttpApi, EF Core on SQLite, BLOBs in
/// memory), and an application whose containers keep their bytes in it over HTTP - the provider on the C# client
/// proxies, sent into this host's test server.
/// </summary>
[DependsOn(
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(AbpAutofacModule),
    typeof(BlobManagementApplicationModule),
    typeof(BlobManagementHttpApiModule),
    typeof(BlobManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(AbpImagingSkiaSharpModule),
    typeof(BlobManagementHttpApiClientModule),
    typeof(AbpBlobStoringBlobManagementModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule)
)]
public class BlobManagementHttpApiTestModule : AbpModule
{
    public const string AllowedClient = "site-app";

    /// <summary>Container that allows <see cref="AllowedClient"/>.</summary>
    public const string OpenContainer = "files";

    /// <summary>Container that allows no client.</summary>
    public const string ClosedContainer = "closed";

    /// <summary>Container with directories.</summary>
    public const string DocumentsContainer = "documents";

    /// <summary>Container that allows <see cref="AllowedClient"/> and is read publicly.</summary>
    public const string PublicContainer = "public";

    /// <summary>The application's container, to <see cref="PublicContainer"/> over HTTP.</summary>
    public const string AppPublicContainer = "app-public";

    /// <summary>Container that allows <see cref="AllowedClient"/>; the ticket tests change its allow-list.</summary>
    public const string TicketsContainer = "tickets";

    /// <summary>The application's container, to <see cref="TicketsContainer"/> over HTTP.</summary>
    public const string AppTicketsContainer = "app-tickets";

    /// <summary>The application's container, to <see cref="OpenContainer"/> over HTTP.</summary>
    public const string AppContainer = "app-remote";

    /// <summary>The application's container, to <see cref="ClosedContainer"/> over HTTP.</summary>
    public const string AppClosedContainer = "app-closed";

    public static readonly Guid TenantId = Guid.Parse("6f3a0c39-41a8-4d43-9a3e-2a35d1f0a001");

    private AbpUnitTestSqliteDatabase? _database;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddAuthentication(options => options.DefaultScheme = TestCallerAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestCallerAuthenticationHandler>(TestCallerAuthenticationHandler.SchemeName, null);

        Configure<AbpMultiTenancyOptions>(options => options.IsEnabled = true);
        Configure<AbpDefaultTenantStoreOptions>(options =>
        {
            options.Tenants = new[] { new TenantConfiguration(TenantId, "test") };
        });

        ConfigureDatabase(context);
        ConfigureBlobStoring(context);

        // The application's side: its IBlobManagementBlobService is the remote one, though Blob Management's in-process one is
        // installed in this host too.
        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Default = new RemoteServiceConfiguration("http://localhost/");
        });
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementBlobService, RemoteBlobManagementBlobService>());
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementTicketService, BlobManagementTicketClientProxy>());

        // The file and container services' generated proxies replace its services wherever HttpApi.Client is installed. This host is
        // the Blob Management too, so its controllers get the in-process services back; the tests call the proxies by their type.
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementContainerAppService, BlobManagementContainerAppService>());
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementFileAppService, BlobManagementFileAppService>());
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementDirectoryAppService, BlobManagementDirectoryAppService>());

        // Blob Management's permission grants, written by the tests straight into the store; the definitions stay in code.
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });

        // What a host provides: the tickets are signed with it. Ephemeral keys, in memory.
        context.Services.AddDataProtection();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseAuthentication();
        app.UseMultiTenancy();
        app.UseUnitOfWork();
        app.UseAuthorization();
        app.UseConfiguredEndpoints();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _database?.Dispose();
    }

    private void ConfigureDatabase(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        _database = new AbpUnitTestSqliteDatabase();
        _database.CreateTables(
            new BlobManagementDbContext(new DbContextOptionsBuilder<BlobManagementDbContext>().UseSqlite(_database.ConnectionString).Options),
            new PermissionManagementDbContext(
                new DbContextOptionsBuilder<PermissionManagementDbContext>().UseSqlite(_database.ConnectionString).Options));

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = _database.ConnectionString;
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext => configurationContext.UseSqlite());
        });
    }

    private static void ConfigureBlobStoring(ServiceConfigurationContext context)
    {
        SingletonMemoryBlobProvider.Register(context.Services);

        context.Services.Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(OpenContainer, container =>
            {
                container.UseBlobManagementContainer(managed => managed.AllowedClientIds = new[] { AllowedClient });
                container.UseMemory();
            });

            options.Containers.Configure(ClosedContainer, container =>
            {
                container.UseBlobManagementContainer();
                container.UseMemory();
            });

            options.Containers.Configure(DocumentsContainer, container =>
            {
                container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
                container.UseMemory();
            });

            options.Containers.Configure(PublicContainer, container =>
            {
                container.UseBlobManagementContainer(managed =>
                {
                    managed.AllowedClientIds = new[] { AllowedClient };
                    managed.PublicRead = true;
                });
                container.UseMemory();
            });

            options.Containers.Configure(TicketsContainer, container =>
            {
                container.UseBlobManagementContainer(managed => managed.AllowedClientIds = new[] { AllowedClient });
                container.UseMemory();
            });

            options.Containers.Configure(AppTicketsContainer, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = TicketsContainer);
            });

            options.Containers.Configure(AppPublicContainer, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = PublicContainer);
            });

            options.Containers.Configure(AppContainer, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = OpenContainer);
            });

            options.Containers.Configure(AppClosedContainer, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = ClosedContainer);
            });
        });
    }
}
