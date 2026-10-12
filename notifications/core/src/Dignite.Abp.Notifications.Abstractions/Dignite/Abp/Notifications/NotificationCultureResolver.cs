using System.Globalization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Picks the culture a channel notifier builds one recipient's content under. Shared by every notifier that renders
/// text outside a request (email, device push), where there is no request culture to inherit.
/// </summary>
/// <remarks>
/// Falls back rather than throws. The recipient's culture name is untrusted input — a value out of a setting store, a
/// device registration, or whatever an application resolver returned — and <see cref="CultureInfo.GetCultureInfo(string)"/>
/// throws for a name that is not well-formed, or for every real culture under invariant globalization. A bad culture
/// name should downgrade that recipient's content to the default culture, not fail the delivery. Switch to the
/// returned culture with ABP's <see cref="CultureHelper.Use(CultureInfo, CultureInfo)"/>, which restores the previous
/// one on dispose.
/// </remarks>
public static class NotificationCultureResolver
{
    /// <param name="cultureName">The recipient's culture name, or null when nothing supplied one.</param>
    /// <param name="defaultCultureName">The notifier's configured default culture.</param>
    /// <param name="defaultCultureSource">
    /// Where <paramref name="defaultCultureName"/> is configured (for example <c>NotificationEmailOptions.DefaultCulture</c>),
    /// named in the warning logged when it is not a valid culture name either.
    /// </param>
    /// <param name="logger">Receives a warning for each invalid culture name encountered.</param>
    public static CultureInfo Resolve(
        string? cultureName,
        string? defaultCultureName,
        string defaultCultureSource,
        ILogger logger)
    {
        var culture = FindOrNull(cultureName);
        if (culture != null)
        {
            return culture;
        }

        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            logger.LogWarning(
                "Recipient culture '{CultureName}' is not a valid culture name; falling back to '{DefaultCulture}'.",
                cultureName,
                defaultCultureName);
        }

        var defaultCulture = FindOrNull(defaultCultureName);
        if (defaultCulture != null)
        {
            return defaultCulture;
        }

        // Also the invariant-globalization path, where no culture name resolves and there is nothing left to fall to.
        logger.LogWarning(
            "{DefaultCultureSource} is '{DefaultCulture}', which is not a valid culture name; falling back to the "
            + "ambient culture '{AmbientCulture}'.",
            defaultCultureSource,
            defaultCultureName,
            CultureInfo.CurrentUICulture.Name);

        return CultureInfo.CurrentUICulture;
    }

    private static CultureInfo? FindOrNull(string? cultureName)
    {
        return cultureName != null && CultureHelper.IsValidCultureCode(cultureName)
            ? CultureInfo.GetCultureInfo(cultureName)
            : null;
    }
}
