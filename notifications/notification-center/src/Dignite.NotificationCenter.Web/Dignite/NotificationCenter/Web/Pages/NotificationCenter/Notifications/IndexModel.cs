using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Dignite.NotificationCenter.Localization;
using Dignite.NotificationCenter.Web.Components.NotificationBell;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Pagination;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Dignite.NotificationCenter.Web.Pages.NotificationCenter.Notifications;

/// <summary>
/// The current user's notification inbox (<c>/NotificationCenter/Notifications</c>): group tabs with unread counts,
/// an all/unread filter, and a paged list. Reached from the bell rather than the main menu. Actions (mark read,
/// delete, mark all read, clear read) run client-side against the inbox API.
/// </summary>
[Authorize]
public class IndexModel : AbpPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? GroupName { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool UnreadOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public virtual int PageSize => 20;

    public IReadOnlyList<UserNotificationGroupDto> Groups { get; protected set; } = new List<UserNotificationGroupDto>();

    /// <summary>Unread notifications across every group (the "All" tab's count).</summary>
    public int TotalUnreadCount { get; protected set; }

    public IReadOnlyList<NotificationBellItemViewModel> Items { get; protected set; } = new List<NotificationBellItemViewModel>();

    public PagerModel PagerModel { get; protected set; } = default!;

    protected IUserNotificationAppService UserNotificationAppService { get; }

    protected NotificationItemViewModelFactory ItemViewModelFactory { get; }

    public IndexModel(
        IUserNotificationAppService userNotificationAppService,
        NotificationItemViewModelFactory itemViewModelFactory)
    {
        UserNotificationAppService = userNotificationAppService;
        ItemViewModelFactory = itemViewModelFactory;
        LocalizationResourceType = typeof(NotificationCenterResource);
    }

    public virtual async Task OnGetAsync()
    {
        Groups = (await UserNotificationAppService.GetGroupsAsync()).Items;
        TotalUnreadCount = Groups.Sum(group => group.UnreadCount);

        CurrentPage = CurrentPage < 1 ? 1 : CurrentPage;
        var result = await UserNotificationAppService.GetListAsync(new GetUserNotificationListInput
        {
            GroupName = GroupName,
            State = UnreadOnly ? UserNotificationState.Unread : null,
            SkipCount = (CurrentPage - 1) * PageSize,
            MaxResultCount = PageSize
        });

        Items = result.Items.Select(ItemViewModelFactory.Create).ToList();
        PagerModel = new PagerModel(
            result.TotalCount, Items.Count, CurrentPage, PageSize, GetPageUrl(GroupName, UnreadOnly));
    }

    /// <summary>URL of this page for a group/filter combination (first page).</summary>
    public virtual string GetPageUrl(string? groupName, bool unreadOnly)
    {
        return Url.Page(
            "/NotificationCenter/Notifications/Index",
            new { groupName, unreadOnly = unreadOnly ? true : (bool?)null })!;
    }
}
