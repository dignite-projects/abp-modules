using Dignite.Abp.Notifications;

namespace Dignite.NotificationCenter.Web;

public static class NotificationSeverityCssExtensions
{
    public static string ToTextColorCssClass(this NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => "text-success",
            NotificationSeverity.Warn => "text-warning",
            NotificationSeverity.Error => "text-danger",
            _ => "text-info"
        };
    }
}
