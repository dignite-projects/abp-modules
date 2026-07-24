using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Emailing;

public class NotificationEmail
{
    public string Subject { get; }

    public string Body { get; }

    public bool IsBodyHtml { get; }

    public NotificationEmail(string subject, string body, bool isBodyHtml = false)
    {
        Subject = subject;
        Body = body;
        IsBodyHtml = isBodyHtml;
    }
}

/// <summary>Turns a notification into an email. Business modules replace this to customize content.</summary>
public interface INotificationEmailBuilder
{
    Task<NotificationEmail?> BuildAsync(
        NotificationEmailBuildContext context,
        CancellationToken cancellationToken = default);
}
