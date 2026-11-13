using Dignite.Abp.BlobStoring.BlobManagement;
using Dignite.Abp.BlobStoring.Pipeline;
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

    /// <summary>The OAuth client the documents container allows; no other container allows any.</summary>
    public const string AllowedClient = "site-app";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
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

            // What the file API asks about a remote caller; the in-process services ask nothing.
            options.Containers.Configure(BlobManagementTestContainers.Documents, container =>
            {
                container.GetBlobManagementContainerConfiguration().AllowedClientIds = new[] { AllowedClient };
            });

            // The application's side: its own containers, keeping their bytes in Blob Management in this process.
            options.Containers.Configure(BlobManagementTestAppContainers.Media, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.Documents, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.PicturesOnly, container =>
            {
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "image/png" });
                container.UseBlobManagement(provider => provider.BlobManagementContainer = BlobManagementTestContainers.Files);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.Images, container =>
            {
                container.UseBlobManagement(provider => provider.BlobManagementContainer = BlobManagementTestContainers.Images);
            });

            options.Containers.Configure(BlobManagementTestAppContainers.HostLevel, container =>
            {
                container.IsMultiTenant = false;
                container.UseBlobManagement(provider => provider.BlobManagementContainer = BlobManagementTestContainers.Files);
            });
        });
    }
}
