using Volo.Abp;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// The resolved delivery information for one notification recipient.
/// </summary>
/// <remarks>
/// A null result from a resolver still means "not mine; try the next resolver". When a resolver claims the
/// recipient, it can provide the culture that should be used while building that recipient's email. The culture is
/// optional so existing address sources can fall back to <see cref="NotificationEmailOptions.DefaultCulture"/>.
/// </remarks>
public sealed class EmailNotificationAddress
{
    public string Address { get; }

    /// <summary>
    /// Optional BCP-47 culture name (for example, <c>en-US</c> or <c>zh-Hans</c>) for this recipient.
    /// </summary>
    public string? CultureName { get; }

    public EmailNotificationAddress(string address, string? cultureName = null)
    {
        Address = Check.NotNullOrWhiteSpace(address, nameof(address));
        CultureName = string.IsNullOrWhiteSpace(cultureName) ? null : cultureName;
    }

    public static EmailNotificationAddress To(string address, string? cultureName = null)
    {
        return new EmailNotificationAddress(address, cultureName);
    }
}
