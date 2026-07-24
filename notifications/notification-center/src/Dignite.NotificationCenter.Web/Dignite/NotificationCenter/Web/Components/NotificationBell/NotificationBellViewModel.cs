using System.Collections.Generic;

namespace Dignite.NotificationCenter.Web.Components.NotificationBell;

public class NotificationBellViewModel
{
    public int UnreadCount { get; }

    public IReadOnlyList<NotificationBellItemViewModel> Items { get; }

    public string SignalRHubUrl { get; }

    public NotificationBellViewModel(int unreadCount, IReadOnlyList<NotificationBellItemViewModel> items, string signalRHubUrl)
    {
        UnreadCount = unreadCount;
        Items = items;
        SignalRHubUrl = signalRHubUrl;
    }
}
