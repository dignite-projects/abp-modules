using Dignite.NotificationCenter.Web.Components.NotificationBell;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Controllers;

[Authorize]
[Route("notification-center/notification-bell")]
public class NotificationBellController : Controller
{
    [HttpGet("dropdown")]
    public IActionResult Dropdown()
    {
        return ViewComponent(typeof(NotificationBellDropdownViewComponent));
    }
}
