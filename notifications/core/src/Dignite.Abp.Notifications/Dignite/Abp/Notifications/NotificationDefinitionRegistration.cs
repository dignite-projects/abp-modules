using System;
using Volo.Abp.Collections;

namespace Dignite.Abp.Notifications;

/// <summary>Registers the notification definition providers to execute at startup, independently from runtime delivery tuning.</summary>
public class NotificationDefinitionRegistration
{
    /// <summary>
    /// Gets definition provider types discovered across application modules or registered explicitly. Repeating the
    /// same provider type is idempotent; the definition manager executes each provider type once.
    /// </summary>
    public ITypeList<INotificationDefinitionProvider> DefinitionProviders { get; }

    public NotificationDefinitionRegistration()
    {
        DefinitionProviders = new TypeList<INotificationDefinitionProvider>();
    }

    internal void Validate()
    {
        foreach (var providerType in DefinitionProviders)
        {
            if (!typeof(INotificationDefinitionProvider).IsAssignableFrom(providerType))
            {
                throw new InvalidOperationException(
                    $"{nameof(NotificationDefinitionRegistration)}.{nameof(DefinitionProviders)} contains " +
                    $"'{providerType.FullName}', which is not a notification definition provider.");
            }
        }
    }
}
