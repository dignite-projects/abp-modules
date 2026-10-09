using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The <see cref="INotificationPublisher"/> of a process with no notification pipeline: a business module can be
/// started — in a test, a tool, a host that does not deliver its notifications — and every publish is logged as a
/// warning and dropped. The shape is ABP's <c>NullSmsSender</c> (and <c>NullBackgroundJobManager</c>'s logger): a
/// singleton with a property-injected logger that defaults to <see cref="NullLogger{T}"/>.
/// </summary>
/// <remarks>
/// A real publisher always wins, whatever the module order: <c>DefaultNotificationPublisher</c> (the in-process
/// pipeline, <c>Dignite.Abp.Notifications</c>) and <c>RemoteNotificationPublisher</c> (<c>Dignite.Abp.Notifications.Client</c>)
/// are registered by their modules, and this one only afterwards, by
/// <see cref="AbpNotificationsAbstractionsModule.PostConfigureServices"/>, and only when nothing else is registered
/// (<c>TryAdd</c>). A conventional <c>TryRegister</c> would run as soon as this package's module is configured — before
/// every other module, since they all depend on it — and would then keep the remote publisher, itself registered with
/// <c>TryRegister</c> so that the local one wins over it, from ever being registered.
/// </remarks>
public class NullNotificationPublisher : INotificationPublisher
{
    public ILogger<NullNotificationPublisher> Logger { get; set; }

    public NullNotificationPublisher()
    {
        Logger = NullLogger<NullNotificationPublisher>.Instance;
    }

    public virtual Task PublishAsync(
        string notificationName,
        NotificationData? data = null,
        NotificationEntityIdentifier? entityIdentifier = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        Guid[]? userIds = null,
        Guid[]? excludedUserIds = null)
    {
        Logger.LogWarning(
            "Notification {NotificationName} was not published: no notification publisher is installed. Install " +
            "Dignite.Abp.Notifications to distribute in this process, or Dignite.Abp.Notifications.Client to hand " +
            "notifications to the process that hosts the inbox.",
            notificationName);

        return Task.CompletedTask;
    }
}
