using Volo.Abp;

namespace Dignite.Abp.Notifications.Push;

/// <summary>The visible text of a push notification, built once per culture.</summary>
/// <remarks>
/// Push text passes through Apple's, Google's and the push provider's servers and shows on a lock screen. Keep it to
/// "there is something new, open the app" — never amounts, grades, or other personal detail.
/// </remarks>
public class NotificationPushContent
{
    /// <summary>Null lets the operating system show the app name instead.</summary>
    public string? Title { get; }

    public string Body { get; }

    public NotificationPushContent(string? title, string body)
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        Body = Check.NotNullOrWhiteSpace(body, nameof(body));
    }
}
