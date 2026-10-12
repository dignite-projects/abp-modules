using Dignite.NotificationCenter.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(AbpLocalizationModule)
    )]
public class AbpNotificationCenterDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AbpNotificationCenterDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<NotificationCenterResource>("en")
                .AddVirtualJson("/Dignite/NotificationCenter/Localization");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("NotificationCenter", typeof(NotificationCenterResource));
        });
    }
}
