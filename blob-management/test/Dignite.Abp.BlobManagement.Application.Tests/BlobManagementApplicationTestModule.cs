using Dignite.Abp.BlobStoring.BlobManagement;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementApplicationModule),
    typeof(AbpBlobStoringBlobManagementModule),
    typeof(BlobManagementDomainTestModule),
    typeof(AbpPermissionManagementDomainModule)
    )]
public class BlobManagementApplicationTestModule : AbpModule
{
    /// <summary>A container <c>BlobManagementContainerSettings_Tests</c> declares in configuration and this module overrides in code.</summary>
    public const string ConfiguredAndOverriddenContainer = "config-media";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The key ring a Blob Management host gets from ASP.NET Core: tickets are signed and read with it.
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        // Grants for the authorization tests, written as users and roles; the definitions stay in code.
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
            options.ManagementProviders.Add<TestUserPermissionManagementProvider>();
            options.ManagementProviders.Add<TestRolePermissionManagementProvider>();
            options.ResourceManagementProviders.Add<TestUserResourcePermissionManagementProvider>();
            options.ResourceManagementProviders.Add<TestRoleResourcePermissionManagementProvider>();
        });

        Configure<AbpBlobStoringOptions>(options =>
        {
            // Code runs after BlobManagementApplicationModule bound the configuration, so it wins - as a host's own code does.
            options.Containers.Configure(ConfiguredAndOverriddenContainer, container =>
            {
                container.GetBlobManagementContainerConfiguration().PublicRead = false;
            });

            // The application's side: its own containers, keeping their bytes in Blob Management in this process.
            options.Containers.Configure(BlobManagementTestAppContainers.Media, container =>
            {
                container.UseBlobManagement(drive => drive.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.Documents, container =>
            {
                container.UseBlobManagement(drive => drive.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.PicturesOnly, container =>
            {
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "image/png" });
                container.UseBlobManagement(drive => drive.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.Images, container =>
            {
                container.UseBlobManagement(drive => drive.BlobManagementContainer = BlobManagementTestContainers.Images);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.HostLevel, container =>
            {
                container.IsMultiTenant = false;
                container.UseBlobManagement(drive => drive.BlobManagementContainer = BlobManagementTestContainers.Files);
            });
        });
    }
}
