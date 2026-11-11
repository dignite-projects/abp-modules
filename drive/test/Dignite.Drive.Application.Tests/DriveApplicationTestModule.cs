using Dignite.Abp.BlobStoring.Drive;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationModule),
    typeof(AbpBlobStoringDriveModule),
    typeof(DriveDomainTestModule),
    typeof(AbpPermissionManagementDomainModule)
    )]
public class DriveApplicationTestModule : AbpModule
{
    /// <summary>A container <c>DriveContainerSettings_Tests</c> declares in configuration and this module overrides in code.</summary>
    public const string ConfiguredAndOverriddenContainer = "config-media";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The key ring a Drive host gets from ASP.NET Core: tickets are signed and read with it.
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        // Grants for the authorization tests, written as users and roles; the definitions stay in code.
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
            options.ManagementProviders.Add<TestUserPermissionManagementProvider>();
            options.ManagementProviders.Add<TestRolePermissionManagementProvider>();
        });

        Configure<AbpBlobStoringOptions>(options =>
        {
            // Code runs after DriveApplicationModule bound the configuration, so it wins - as a host's own code does.
            options.Containers.Configure(ConfiguredAndOverriddenContainer, container =>
            {
                container.GetDriveContainerConfiguration().PublicRead = false;
            });

            // The application's side: its own containers, keeping their bytes in the Drive in this process.
            options.Containers.Configure(DriveTestAppContainers.Media, container =>
            {
                container.UseDrive(drive => drive.DriveContainer = DriveTestContainers.Files);
            });

            options.Containers.Configure(DriveTestAppContainers.Documents, container =>
            {
                container.UseDrive(drive => drive.DriveContainer = DriveTestContainers.Files);
            });

            options.Containers.Configure(DriveTestAppContainers.PicturesOnly, container =>
            {
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "image/png" });
                container.UseDrive(drive => drive.DriveContainer = DriveTestContainers.Files);
            });

            options.Containers.Configure(DriveTestAppContainers.Images, container =>
            {
                container.UseDrive(drive => drive.DriveContainer = DriveTestContainers.Images);
            });

            options.Containers.Configure(DriveTestAppContainers.HostLevel, container =>
            {
                container.IsMultiTenant = false;
                container.UseDrive(drive => drive.DriveContainer = DriveTestContainers.Files);
            });
        });
    }
}
