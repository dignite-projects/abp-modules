using System;
using Dignite.Abp.Notifications;
using Volo.Abp.Application.Dtos;

namespace Dignite.NotificationCenter;

public class GetUserNotificationListInput : PagedResultRequestDto
{
    public UserNotificationState? State { get; set; }

    /// <summary>
    /// Keeps only notifications of this group (see <see cref="IUserNotificationAppService.GetGroupsAsync"/>), including
    /// <see cref="NotificationCenterConsts.OtherGroupName"/>. <see langword="null"/> means every group.
    /// </summary>
    public string? GroupName { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}
