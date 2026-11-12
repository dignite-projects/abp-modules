using Volo.Abp.Modularity;
using Volo.Abp.Localization;
using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Domain;
using Volo.Abp.Features;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(AbpValidationModule),
    typeof(AbpFeaturesModule),
    typeof(AbpDddDomainSharedModule)
)]
public class BlobManagementDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BlobManagementDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<BlobManagementResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Dignite/Abp/BlobManagement/Localization/BlobManagement");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace(BlobManagementErrorCodes.Namespace, typeof(BlobManagementResource));
        });
    }
}
