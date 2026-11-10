using Volo.Abp.Modularity;
using Volo.Abp.Localization;
using Dignite.Drive.Localization;
using Volo.Abp.Domain;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpValidationModule),
    typeof(AbpDddDomainSharedModule)
)]
public class DriveDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DriveDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<DriveResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/Drive");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("Drive", typeof(DriveResource));
        });
    }
}
