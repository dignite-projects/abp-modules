using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.NotificationCenter;

/// <summary>Headless inbox API for the current user's received notifications. Subscriptions live on
/// <see cref="INotificationSubscriptionAppService"/>.</summary>
public interface IUserNotificationAppService : IApplicationService
{
    Task<PagedResultDto<UserNotificationDto>> GetListAsync(GetUserNotificationListInput input);

    /// <summary>Gets the current user's unread notification count (for the bell badge). Any other count comes from
    /// <see cref="GetListAsync"/>'s <c>TotalCount</c> with the desired state filter.</summary>
    Task<int> GetUnreadCountAsync();

    Task MarkAsReadAsync(Guid notificationId);

    Task MarkAllAsReadAsync();

    Task DeleteAsync(Guid notificationId);

    /// <summary>Deletes all of the current user's read notifications. Unread ones are preserved — delete those
    /// individually via <see cref="DeleteAsync"/>.</summary>
    Task DeleteAllReadAsync();
}
