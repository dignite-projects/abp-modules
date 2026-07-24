using System;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Single source of truth mapping stable discriminators to <see cref="NotificationData"/> CLR types.
/// Used by serialization on both the writing (server) and reading (remote client) sides. Discriminator
/// registration and lookup both use ordinal, case-sensitive comparison.
/// </summary>
public interface INotificationDataTypeRegistry
{
    string? GetDiscriminatorOrNull(Type dataType);

    Type? GetTypeOrNull(string discriminator);
}
