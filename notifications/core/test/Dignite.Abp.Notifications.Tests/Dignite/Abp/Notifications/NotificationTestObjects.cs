using System;
using Microsoft.Extensions.Options;

namespace Dignite.Abp.Notifications;

/// <summary>A custom notification payload standing in for one a business module would define.</summary>
[NotificationDataType("Test.OrderShipped")]
public class OrderShippedNotificationData : NotificationData
{
    public string OrderNumber { get; set; } = default!;

    public int ItemCount { get; set; }
}

[NotificationDataType("Test.ThrowingSetter")]
public class ThrowingSetterNotificationData : NotificationData
{
    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (string.Equals(value, "bad", StringComparison.Ordinal))
            {
                throw new FormatException("The historical value is invalid.");
            }

            _value = value;
        }
    }
}

internal static class NotificationTestObjects
{
    public static NotificationDataTypeRegistry CreateRegistry(params Type[] extraTypes)
    {
        var options = CreateBaseOptions();
        foreach (var type in extraTypes)
        {
            options.Add(type);
        }

        return new NotificationDataTypeRegistry(Options.Create(options));
    }

    public static NotificationDataSerializer CreateSerializer(params Type[] extraTypes)
    {
        return new NotificationDataSerializer(CreateRegistry(extraTypes));
    }

    private static NotificationDataOptions CreateBaseOptions()
    {
        var options = new NotificationDataOptions();
        options.Add<MessageNotificationData>();
        options.Add<LocalizableMessageNotificationData>();
        options.Add<UnsupportedNotificationData>();
        return options;
    }
}
