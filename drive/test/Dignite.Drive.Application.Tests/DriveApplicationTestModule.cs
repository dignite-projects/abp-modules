using Dignite.Abp.BlobStoring.Drive;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationModule),
    typeof(AbpBlobStoringDriveModule),
    typeof(DriveDomainTestModule)
    )]
public class DriveApplicationTestModule : AbpModule
{
    /// <summary>A container <c>DriveContainerSettings_Tests</c> declares in configuration and this module overrides in code.</summary>
    public const string ConfiguredAndOverriddenContainer = "config-media";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
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
