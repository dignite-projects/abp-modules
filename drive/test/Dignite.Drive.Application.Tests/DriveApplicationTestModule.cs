using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationModule),
    typeof(DriveDomainTestModule)
    )]
public class DriveApplicationTestModule : AbpModule
{
    /// <summary>A container <c>DriveContainerSettings_Tests</c> declares in configuration and this module overrides in code.</summary>
    public const string ConfiguredAndOverriddenContainer = "config-media";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Code runs after DriveApplicationModule bound the configuration, so it wins - as a host's own code does.
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(ConfiguredAndOverriddenContainer, container =>
            {
                container.GetDriveContainerConfiguration().PublicRead = false;
            });
        });
    }
}
