using System;
using Dignite.Abp.Notifications;

namespace Dignite.NotificationCenter;

/// <summary>A custom notification payload standing in for one a business module would define.</summary>
[NotificationDataType("Test.OrderShipped")]
public class OrderShippedNotificationData : NotificationData
{
    private string _orderNumber = default!;

    public string OrderNumber
    {
        get => _orderNumber;
        set
        {
            if (string.Equals(value, "THROW-FORMAT", StringComparison.Ordinal))
            {
                throw new FormatException("The historical order number is invalid.");
            }

            _orderNumber = value;
        }
    }

    public int ItemCount { get; set; }
}
