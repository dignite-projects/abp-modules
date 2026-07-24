namespace Dignite.Abp.Notifications;

/// <summary>
/// Implemented by business modules to register their notification types.
/// </summary>
public interface INotificationDefinitionProvider
{
    void Define(INotificationDefinitionContext context);
}
