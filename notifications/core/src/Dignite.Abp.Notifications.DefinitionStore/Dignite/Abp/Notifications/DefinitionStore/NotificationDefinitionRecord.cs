using System;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// A saved <see cref="NotificationDefinition"/>. The shape, <see cref="HasSameData"/> and <see cref="Patch"/> are ABP's
/// <c>PermissionDefinitionRecord</c>'s. <see cref="DisplayName"/> and <see cref="Description"/> are serialized
/// <c>ILocalizableString</c>s, so the reading process resolves them by resource name. <see cref="PermissionName"/> and
/// <see cref="FeatureName"/> are the delivery requirements the distributor applies (the event carries none);
/// <see cref="NotificationDefinition.Attributes"/> are kept in <see cref="ExtraProperties"/>.
/// </summary>
public class NotificationDefinitionRecord : BasicAggregateRoot<Guid>, IHasExtraProperties
{
    public string GroupName { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    public string? PermissionName { get; set; }

    public string? FeatureName { get; set; }

    public ExtraPropertyDictionary ExtraProperties { get; protected set; }

    public NotificationDefinitionRecord()
    {
        ExtraProperties = new ExtraPropertyDictionary();
        this.SetDefaultsForExtraProperties();
    }

    public NotificationDefinitionRecord(
        Guid id,
        string groupName,
        string name,
        string displayName,
        string? description = null,
        string? permissionName = null,
        string? featureName = null)
        : base(id)
    {
        GroupName = Check.NotNullOrWhiteSpace(groupName, nameof(groupName), NotificationGroupDefinitionRecordConsts.MaxNameLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), NotificationDefinitionRecordConsts.MaxNameLength);
        DisplayName = Check.NotNullOrWhiteSpace(
            displayName, nameof(displayName), NotificationDefinitionRecordConsts.MaxDisplayNameLength);
        Description = Check.Length(description, nameof(description), NotificationDefinitionRecordConsts.MaxDescriptionLength);
        PermissionName = Check.Length(
            permissionName, nameof(permissionName), NotificationDefinitionRecordConsts.MaxPermissionNameLength);
        FeatureName = Check.Length(featureName, nameof(featureName), NotificationDefinitionRecordConsts.MaxFeatureNameLength);
        ExtraProperties = new ExtraPropertyDictionary();
        this.SetDefaultsForExtraProperties();
    }

    public bool HasSameData(NotificationDefinitionRecord otherRecord)
    {
        if (Name != otherRecord.Name)
        {
            return false;
        }

        if (GroupName != otherRecord.GroupName)
        {
            return false;
        }

        if (DisplayName != otherRecord.DisplayName)
        {
            return false;
        }

        if (Description != otherRecord.Description)
        {
            return false;
        }

        if (PermissionName != otherRecord.PermissionName)
        {
            return false;
        }

        if (FeatureName != otherRecord.FeatureName)
        {
            return false;
        }

        if (!this.HasSameExtraProperties(otherRecord))
        {
            return false;
        }

        return true;
    }

    public void Patch(NotificationDefinitionRecord otherRecord)
    {
        if (Name != otherRecord.Name)
        {
            Name = otherRecord.Name;
        }

        if (GroupName != otherRecord.GroupName)
        {
            GroupName = otherRecord.GroupName;
        }

        if (DisplayName != otherRecord.DisplayName)
        {
            DisplayName = otherRecord.DisplayName;
        }

        if (Description != otherRecord.Description)
        {
            Description = otherRecord.Description;
        }

        if (PermissionName != otherRecord.PermissionName)
        {
            PermissionName = otherRecord.PermissionName;
        }

        if (FeatureName != otherRecord.FeatureName)
        {
            FeatureName = otherRecord.FeatureName;
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
