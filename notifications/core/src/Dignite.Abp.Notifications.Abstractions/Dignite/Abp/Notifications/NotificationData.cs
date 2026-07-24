using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Base type for all notification payloads. Concrete subclasses declare a stable discriminator via
/// <see cref="NotificationDataTypeAttribute"/> and are (de)serialized polymorphically through
/// <see cref="INotificationDataSerializer"/> — never via CLR type names / AssemblyQualifiedName.
/// </summary>
public abstract class NotificationData
{
    /// <summary>
    /// Captures JSON properties not mapped to a CLR property. Ensures forward-compatible reads instead of
    /// silent data loss.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
