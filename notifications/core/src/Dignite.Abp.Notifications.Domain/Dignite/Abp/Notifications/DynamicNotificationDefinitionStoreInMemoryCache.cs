using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Rebuilds definitions from records, as ABP's <c>DynamicPermissionDefinitionStoreInMemoryCache</c>: through a
/// <see cref="NotificationDefinitionContext"/>, so the rebuilt definitions are the same types static ones are. A
/// definition record whose group has no record is skipped, as ABP skips a permission without its group.
/// </summary>
/// <remarks>
/// Display texts are deserialized with <see cref="ILocalizableStringSerializer"/>: a <c>L:Resource,Key</c> text becomes
/// a <see cref="LocalizableString"/> by resource name, which ABP resolves from this process's resources or, failing
/// that, from <c>IExternalLocalizationStore</c> — the Language Management store in a microservice solution.
/// </remarks>
public class DynamicNotificationDefinitionStoreInMemoryCache :
    IDynamicNotificationDefinitionStoreInMemoryCache,
    ISingletonDependency
{
    public string? CacheStamp { get; set; }

    protected List<NotificationGroupDefinition> NotificationGroupDefinitions { get; }

    protected Dictionary<string, NotificationDefinition> NotificationDefinitions { get; }

    protected ILocalizableStringSerializer LocalizableStringSerializer { get; }

    public SemaphoreSlim SyncSemaphore { get; } = new(1, 1);

    public DateTime? LastCheckTime { get; set; }

    public DynamicNotificationDefinitionStoreInMemoryCache(ILocalizableStringSerializer localizableStringSerializer)
    {
        LocalizableStringSerializer = localizableStringSerializer;
        NotificationGroupDefinitions = new List<NotificationGroupDefinition>();
        NotificationDefinitions = new Dictionary<string, NotificationDefinition>(StringComparer.Ordinal);
    }

    public Task FillAsync(
        List<NotificationGroupDefinitionRecord> notificationGroupRecords,
        List<NotificationDefinitionRecord> notificationRecords)
    {
        NotificationGroupDefinitions.Clear();
        NotificationDefinitions.Clear();

        var context = new NotificationDefinitionContext();

        foreach (var notificationGroupRecord in notificationGroupRecords)
        {
            var notificationGroup = context.AddGroup(
                notificationGroupRecord.Name,
                LocalizableStringSerializer.Deserialize(notificationGroupRecord.DisplayName));

            NotificationGroupDefinitions.Add(notificationGroup);

            foreach (var notificationRecord in notificationRecords.Where(x => x.GroupName == notificationGroup.Name))
            {
                var notification = notificationGroup.AddNotification(
                    notificationRecord.Name,
                    LocalizableStringSerializer.Deserialize(notificationRecord.DisplayName));

                ApplyNotificationProperties(notification, notificationRecord);

                NotificationDefinitions[notification.Name] = notification;
            }
        }

        return Task.CompletedTask;
    }

    public NotificationDefinition? GetNotificationOrNull(string name)
    {
        return NotificationDefinitions.GetOrDefault(name);
    }

    public IReadOnlyList<NotificationDefinition> GetNotifications()
    {
        return NotificationDefinitions.Values.ToList();
    }

    public IReadOnlyList<NotificationGroupDefinition> GetGroups()
    {
        return NotificationGroupDefinitions.ToList();
    }

    protected virtual void ApplyNotificationProperties(
        NotificationDefinition notification,
        NotificationDefinitionRecord notificationRecord)
    {
        if (notificationRecord.Description != null)
        {
            notification.WithDescription(LocalizableStringSerializer.Deserialize(notificationRecord.Description));
        }

        if (!string.IsNullOrWhiteSpace(notificationRecord.PermissionName))
        {
            notification.RequirePermission(notificationRecord.PermissionName);
        }

        if (!string.IsNullOrWhiteSpace(notificationRecord.FeatureName))
        {
            notification.RequireFeature(notificationRecord.FeatureName);
        }

        foreach (var property in notificationRecord.ExtraProperties)
        {
            notification.WithAttribute(property.Key, property.Value);
        }
    }
}
