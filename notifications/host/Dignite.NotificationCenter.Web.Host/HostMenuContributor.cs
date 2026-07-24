using System.Threading.Tasks;
using Volo.Abp.UI.Navigation;

namespace Dignite.NotificationCenter.Web.Host;

/// <summary>
/// Adds the demo notifications test page (mirrors the Angular demo's notifications page) to the main menu so
/// it's actually reachable, not just a URL you have to know.
/// </summary>
public class HostMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main)
        {
            return Task.CompletedTask;
        }

        context.Menu.AddItem(
            new ApplicationMenuItem(
                "Host.Notifications",
                "Notifications",
                url: "~/Notifications",
                icon: "fas fa-bell"
            )
        );

        return Task.CompletedTask;
    }
}
