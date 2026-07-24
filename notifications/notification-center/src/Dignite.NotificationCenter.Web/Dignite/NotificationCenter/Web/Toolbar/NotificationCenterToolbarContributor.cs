using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;

namespace Dignite.NotificationCenter.Web.Toolbar;

public class NotificationCenterToolbarContributor : IToolbarContributor
{
    public virtual Task ConfigureToolbarAsync(IToolbarConfigurationContext context)
    {
        if (context.Toolbar.Name != StandardToolbars.Main)
        {
            return Task.CompletedTask;
        }

        context.Toolbar.Items.Add(
            new ToolbarItem(typeof(Components.NotificationBell.NotificationBellViewComponent))
        );

        return Task.CompletedTask;
    }
}
