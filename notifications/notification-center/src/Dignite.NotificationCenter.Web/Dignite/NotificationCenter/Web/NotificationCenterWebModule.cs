using Dignite.NotificationCenter.Web.Settings;
using Dignite.NotificationCenter.Web.Toolbar;
using Dignite.Abp.Notifications;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.SettingManagement.Web.Pages.SettingManagement;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.NotificationCenter.Web;

[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpSettingManagementWebModule)
    )]
public class NotificationCenterWebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<NotificationCenterWebModule>();
        });

        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                NotificationCenterWebConsts.GlobalBundleName,
                bundle => bundle.AddFiles("/Dignite/NotificationCenter/Web/notification-center.css"));

            options.ScriptBundles.Configure(
                NotificationCenterWebConsts.GlobalBundleName,
                bundle => bundle.AddFiles("/Dignite/NotificationCenter/Web/notification-center.js"));
        });

        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new NotificationCenterToolbarContributor());
        });

        // Subscriptions management lives as a tab on the shared Settings page (see
        // https://abp.io/docs/latest/modules/setting-management) rather than its own top-level menu item.
        Configure<SettingManagementPageOptions>(options =>
        {
            options.Contributors.Add(new NotificationCenterSettingPageContributor());
        });

        Configure<NotificationCenterWebOptions>(options =>
        {
            options.DataViewComponents[MessageDiscriminator] =
                typeof(Components.MessageNotificationData.MessageNotificationDataViewComponent);
            options.DataViewComponents[LocalizableMessageDiscriminator] =
                typeof(Components.LocalizableMessageNotificationData.LocalizableMessageNotificationDataViewComponent);
            options.DataViewComponents[UnsupportedDiscriminator] =
                typeof(Components.UnsupportedNotificationData.UnsupportedNotificationDataViewComponent);
        });
    }

    private static readonly string MessageDiscriminator =
        NotificationDataTypeAttribute.GetNameOrNull(typeof(MessageNotificationData))!;

    private static readonly string LocalizableMessageDiscriminator =
        NotificationDataTypeAttribute.GetNameOrNull(typeof(LocalizableMessageNotificationData))!;

    private static readonly string UnsupportedDiscriminator =
        NotificationDataTypeAttribute.GetNameOrNull(typeof(UnsupportedNotificationData))!;
}
