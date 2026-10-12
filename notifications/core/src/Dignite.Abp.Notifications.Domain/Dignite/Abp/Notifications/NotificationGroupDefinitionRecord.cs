using System;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A saved <see cref="NotificationGroupDefinition"/>. The shape, <see cref="HasSameData"/> and <see cref="Patch"/> are
/// ABP's <c>PermissionGroupDefinitionRecord</c>'s; <see cref="DisplayName"/> is the serialized
/// <c>ILocalizableString</c>.
/// </summary>
public class NotificationGroupDefinitionRecord : BasicAggregateRoot<Guid>, IHasExtraProperties
{
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public ExtraPropertyDictionary ExtraProperties { get; protected set; }

    public NotificationGroupDefinitionRecord()
    {
        ExtraProperties = new ExtraPropertyDictionary();
        this.SetDefaultsForExtraProperties();
    }

    public NotificationGroupDefinitionRecord(Guid id, string name, string displayName)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), NotificationGroupDefinitionRecordConsts.MaxNameLength);
        DisplayName = Check.NotNullOrWhiteSpace(
            displayName, nameof(displayName), NotificationGroupDefinitionRecordConsts.MaxDisplayNameLength);
        ExtraProperties = new ExtraPropertyDictionary();
        this.SetDefaultsForExtraProperties();
    }

    public bool HasSameData(NotificationGroupDefinitionRecord otherRecord)
    {
        if (Name != otherRecord.Name)
        {
            return false;
        }

        if (DisplayName != otherRecord.DisplayName)
        {
            return false;
        }

        if (!this.HasSameExtraProperties(otherRecord))
        {
            return false;
        }

        return true;
    }

    public void Patch(NotificationGroupDefinitionRecord otherRecord)
    {
        if (Name != otherRecord.Name)
        {
            Name = otherRecord.Name;
        }

        if (DisplayName != otherRecord.DisplayName)
        {
            DisplayName = otherRecord.DisplayName;
        }

        if (!this.HasSameExtraProperties(otherRecord))
        {
            ExtraProperties.Clear();

            foreach (var property in otherRecord.ExtraProperties)
            {
                ExtraProperties.Add(property.Key, property.Value);
            }
        }
    }
}
