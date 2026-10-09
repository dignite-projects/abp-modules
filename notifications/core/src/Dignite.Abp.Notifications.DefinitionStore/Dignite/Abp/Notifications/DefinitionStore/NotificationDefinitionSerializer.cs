using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// ABP's <c>PermissionDefinitionSerializer</c>, for notification definitions: display texts through
/// <see cref="ILocalizableStringSerializer"/> (<c>L:Resource,Key</c>, so the reading process resolves them by resource
/// name, or <c>F:text</c>), the requirements as names, and the attributes into <c>ExtraProperties</c>.
/// </summary>
/// <remarks>
/// An attribute is saved only when its value is a JSON scalar — a string, a boolean, a number, a
/// <see cref="Guid"/>, a date or time — or null, the values the store's JSON column reads back as the same value. Any
/// other value (an object, a collection, a type, an enum) stays local to the process that defines it, where the static
/// definition keeps it.
/// </remarks>
public class NotificationDefinitionSerializer : INotificationDefinitionSerializer, ITransientDependency
{
    protected IGuidGenerator GuidGenerator { get; }

    protected ILocalizableStringSerializer LocalizableStringSerializer { get; }

    public NotificationDefinitionSerializer(
        IGuidGenerator guidGenerator,
        ILocalizableStringSerializer localizableStringSerializer)
    {
        GuidGenerator = guidGenerator;
        LocalizableStringSerializer = localizableStringSerializer;
    }

    public virtual async Task<(NotificationGroupDefinitionRecord[], NotificationDefinitionRecord[])> SerializeAsync(
        IEnumerable<NotificationGroupDefinition> notificationGroups)
    {
        var groupRecords = new List<NotificationGroupDefinitionRecord>();
        var notificationRecords = new List<NotificationDefinitionRecord>();

        foreach (var notificationGroup in notificationGroups)
        {
            groupRecords.Add(await SerializeAsync(notificationGroup));

            foreach (var notification in notificationGroup.Notifications)
            {
                notificationRecords.Add(await SerializeAsync(notification));
            }
        }

        return (groupRecords.ToArray(), notificationRecords.ToArray());
    }

    public virtual Task<NotificationGroupDefinitionRecord> SerializeAsync(NotificationGroupDefinition notificationGroup)
    {
        using (CultureHelper.Use(CultureInfo.InvariantCulture))
        {
            return Task.FromResult(new NotificationGroupDefinitionRecord(
                GuidGenerator.Create(),
                notificationGroup.Name,
                LocalizableStringSerializer.Serialize(notificationGroup.DisplayName)!));
        }
    }

    public virtual Task<NotificationDefinitionRecord> SerializeAsync(NotificationDefinition notification)
    {
        using (CultureHelper.Use(CultureInfo.InvariantCulture))
        {
            var record = new NotificationDefinitionRecord(
                GuidGenerator.Create(),
                notification.GroupName,
                notification.Name,
                LocalizableStringSerializer.Serialize(notification.DisplayName)!,
                notification.Description == null ? null : LocalizableStringSerializer.Serialize(notification.Description),
                NullIfEmpty(notification.PermissionName),
                NullIfEmpty(notification.FeatureName));

            foreach (var attribute in notification.Attributes)
            {
                if (IsJsonScalar(attribute.Value))
                {
                    record.SetProperty(attribute.Key, attribute.Value);
                }
            }

            return Task.FromResult(record);
        }
    }

    /// <summary>Whether an attribute value is saved; see the class remarks.</summary>
    protected virtual bool IsJsonScalar(object? value)
    {
        return value is null
            or string
            or bool
            or char
            or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal
            or Guid
            or DateTime or DateTimeOffset or TimeSpan;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
