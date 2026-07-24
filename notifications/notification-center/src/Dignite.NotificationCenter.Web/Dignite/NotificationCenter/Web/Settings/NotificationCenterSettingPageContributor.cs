using System.Threading.Tasks;
using Dignite.NotificationCenter.Localization;
using Dignite.NotificationCenter.Web.Components.NotificationSubscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Volo.Abp.SettingManagement.Web.Pages.SettingManagement;

namespace Dignite.NotificationCenter.Web.Settings;

public class NotificationCenterSettingPageContributor : SettingPageContributorBase
{
    public override Task ConfigureAsync(SettingPageCreationContext context)
    {
        var l = context.ServiceProvider.GetRequiredService<IStringLocalizer<NotificationCenterResource>>();

        context.Groups.Add(new SettingPageGroup(
            NotificationCenterWebConsts.SettingGroupName,
            l["Subscriptions"],
            typeof(NotificationSubscriptionsViewComponent)));

        return Task.CompletedTask;
    }
}
