using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

[ExposeServices(
    typeof(INotificationDataSerializer),
    typeof(NotificationDataSerializer))]
public class NotificationDataSerializer :
    INotificationDataSerializer,
    ISingletonDependency
{
    private readonly JsonSerializerOptions _options;

    public NotificationDataSerializer(INotificationDataTypeRegistry registry)
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _options.Converters.Add(new NotificationDataJsonConverter(registry));
    }

    public string? Serialize(NotificationData? data)
    {
        return data is null ? null : JsonSerializer.Serialize(data, _options);
    }

    public NotificationData? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<NotificationData>(json!, _options);
            if (data is UnsupportedNotificationData { RawJson.Length: 0 } unsupported)
            {
                unsupported.RawJson = json!;
            }

            return data;
        }
        catch (Exception exception) when (NotificationDataJsonConverter.IsRecoverableReadException(exception))
        {
            return new UnsupportedNotificationData
            {
                Reason = UnsupportedNotificationDataReason.MalformedPayload,
                RawJson = json!
            };
        }
    }
}
