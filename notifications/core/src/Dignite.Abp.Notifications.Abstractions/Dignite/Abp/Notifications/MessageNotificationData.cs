namespace Dignite.Abp.Notifications;

/// <summary>A plain, pre-formatted text message.</summary>
[NotificationDataType("Dignite.Message")]
public class MessageNotificationData : NotificationData
{
    public string Message { get; set; } = default!;

    public MessageNotificationData()
    {
    }

    public MessageNotificationData(string message)
    {
        Message = message;
    }
}
