using System;
using Dignite.Abp.Notifications;
using Volo.Abp.Application.Dtos;

namespace Dignite.NotificationCenter;

public class GetUserNotificationListInput : PagedResultRequestDto
{
    public UserNotificationState? State { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}
