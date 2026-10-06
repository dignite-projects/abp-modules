using System;

namespace Dignite.NotificationCenter;

/// <summary>
/// Produces non-null, ordinal identity keys for subscription persistence. The keys make host-tenant and nullable
/// entity scopes behave consistently in unique indexes across relational databases and MongoDB.
/// </summary>
public static class NotificationSubscriptionIdentity
{
    public static Guid GetTenantKey(Guid? tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant identifier cannot be Guid.Empty.", nameof(tenantId));
        }

        return tenantId ?? Guid.Empty;
    }

    public static string GetNotificationNameKey(string notificationName)
    {
        return NotificationCenterIdentityKey.Compute("N", CheckRequiredLength(
            notificationName,
            nameof(notificationName),
            NotificationCenterConsts.MaxNotificationNameLength));
    }

    public static string GetScopeKey(string? entityTypeName, string? entityId)
    {
        if (entityTypeName == null && entityId == null)
        {
            return NotificationCenterIdentityKey.Compute("D");
        }

        if (entityTypeName == null || entityId == null)
        {
            throw new ArgumentException(
                "EntityTypeName and EntityId must either both be null or both be non-null.");
        }

        var checkedEntityTypeName = CheckRequiredLength(
            entityTypeName,
            nameof(entityTypeName),
            NotificationCenterConsts.MaxEntityTypeNameLength);
        var checkedEntityId = CheckRequiredLength(
            entityId,
            nameof(entityId),
            NotificationCenterConsts.MaxEntityIdLength);

        return NotificationCenterIdentityKey.Compute("E", checkedEntityTypeName, checkedEntityId);
    }

    private static string CheckRequiredLength(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot be longer than {maxLength} characters.",
                parameterName);
        }

        return value;
    }
}
