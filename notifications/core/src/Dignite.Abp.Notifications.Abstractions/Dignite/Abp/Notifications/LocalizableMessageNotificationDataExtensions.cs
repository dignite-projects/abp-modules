using System.Linq;
using Microsoft.Extensions.Localization;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

public static class LocalizableMessageNotificationDataExtensions
{
    /// <summary>
    /// Renders the message in the current UI culture: its resource's localizer (or the default resource's), with the
    /// arguments applied positionally. Falls back to the raw key when no localizer is available. The one rendering
    /// rule every channel and UI shares, so an inbox entry, an email and a push say the same thing.
    /// </summary>
    public static string Localize(
        this LocalizableMessageNotificationData data,
        IStringLocalizerFactory stringLocalizerFactory)
    {
        Check.NotNull(data, nameof(data));
        Check.NotNull(stringLocalizerFactory, nameof(stringLocalizerFactory));

        var localizer = data.ResourceName != null
            ? stringLocalizerFactory.CreateByResourceNameOrNull(data.ResourceName)
            : null;
        localizer ??= stringLocalizerFactory.CreateDefaultOrNull();

        return localizer == null
            ? data.Name
            : data.Arguments != null
                ? localizer[data.Name, data.Arguments.Values.ToArray()].Value
                : localizer[data.Name].Value;
    }
}
