using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Emailing;

public interface INotificationEmailContentProvider
{
    int Order { get; }

    Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        CancellationToken cancellationToken = default);
}
