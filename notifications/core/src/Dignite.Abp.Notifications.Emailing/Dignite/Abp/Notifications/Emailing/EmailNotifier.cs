using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Relays notifications to email — the second notifier that stress-tests the framework's event contract. Unlike
/// SignalR (which addresses users directly), email needs a UserId → address mapping, supplied by the
/// <see cref="IEmailNotificationAddressResolver"/> chain. Honors channel routing via <see cref="NotificationChannels"/>.
/// Delivery is best-effort: a recipient without a resolvable address or email content is skipped with a log entry.
/// </summary>
[ExposeServices(
    typeof(INotificationNotifier),
    typeof(EmailNotifier))]
public class EmailNotifier :
    INotificationNotifier,
    ITransientDependency
{
    public const string ChannelName = "Email";

    public string Name => ChannelName;

    protected IEmailSender EmailSender { get; }

    protected IReadOnlyList<IEmailNotificationAddressResolver> AddressResolvers { get; }

    protected INotificationEmailBuilder EmailBuilder { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    protected ILogger<EmailNotifier> Logger { get; }

    protected NotificationEmailOptions EmailOptions { get; }

    public EmailNotifier(
        IEmailSender emailSender,
        IEnumerable<IEmailNotificationAddressResolver> addressResolvers,
        INotificationEmailBuilder emailBuilder,
        INotificationDataSerializer dataSerializer,
        ILogger<EmailNotifier> logger,
        IOptions<NotificationEmailOptions> emailOptions)
    {
        EmailSender = emailSender;
        EmailBuilder = emailBuilder;
        DataSerializer = dataSerializer;
        Logger = logger;
        EmailOptions = emailOptions.Value;

        // Same Order-then-FullName-Ordinal tiebreak DefaultNotificationEmailBuilder uses for content providers, so
        // which address a user receives never depends on DI registration order.
        AddressResolvers = addressResolvers
            .OrderBy(resolver => resolver.Order)
            .ThenBy(resolver => resolver.GetType().FullName, StringComparer.Ordinal)
            .ToList();

        if (AddressResolvers.Count == 0)
        {
            // This notifier is transient, so the chain is inspected once per notifier instance rather than per call.
            Logger.LogWarning(
                "Notification emailing is installed but no {Resolver} is registered, so no notification emails will "
                + "be sent. Install Dignite.Abp.Notifications.Emailing.Identity or register your own resolver.",
                nameof(IEmailNotificationAddressResolver));
        }
    }

    public virtual Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Channel, Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {nameof(EmailNotifier)} cannot deliver channel '{request.Channel}'.");
        }

        return DeliverToUserAsync(
            NotificationPayload.FromRequest(request, DataSerializer),
            request.UserId,
            request.TenantId,
            cancellationToken);
    }

    protected virtual async Task DeliverToUserAsync(
        NotificationPayload notification,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new EmailNotificationAddressResolveContext(notification, userId, tenantId);
        var address = await ResolveAddressOrNullAsync(context, cancellationToken);
        if (address == null)
        {
            Logger.LogDebug(
                "No email address resolved for notification '{NotificationName}' and user '{UserId}'; skipping email delivery.",
                notification.NotificationName,
                userId);
            return;
        }

        var culture = ResolveCulture(address.CultureName);
        NotificationEmail? email;

        // CultureInfo is backed by AsyncLocal. Set it only around this recipient's content build and always restore
        // both values so another delivery cannot inherit the previous recipient's culture.
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            email = await EmailBuilder.BuildAsync(
                new NotificationEmailBuildContext(
                    notification,
                    userId,
                    address.Address,
                    tenantId,
                    culture.Name),
                cancellationToken);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }

        if (email == null)
        {
            Logger.LogDebug(
                "No email content provider produced content for notification '{NotificationName}' and user '{UserId}'.",
                notification.NotificationName,
                userId);
            return;
        }

        // ABP's IEmailSender has no CancellationToken overload. Observe cancellation immediately before entering
        // that non-cancellable provider boundary; a provider-specific sender can implement cancellation internally.
        cancellationToken.ThrowIfCancellationRequested();
        await EmailSender.SendAsync(address.Address, email.Subject, email.Body, email.IsBodyHtml);
    }

    /// <summary>Walks the resolver chain and takes the first non-null address result.</summary>
    protected virtual async Task<EmailNotificationAddress?> ResolveAddressOrNullAsync(
        EmailNotificationAddressResolveContext context,
        CancellationToken cancellationToken)
    {
        foreach (var resolver in AddressResolvers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var address = await resolver.GetEmailOrNullAsync(context, cancellationToken);
            if (address != null)
            {
                return address;
            }
        }

        return null;
    }

    /// <summary>
    /// Falls back rather than throws. The culture name is untrusted input — a value out of the setting store, or
    /// whatever an application resolver returned — and <see cref="CultureInfo.GetCultureInfo(string)"/> throws for a
    /// name that is not well-formed, or for every real culture under invariant globalization. A bad stored culture
    /// name should downgrade this recipient's email to the default culture, not fail the delivery.
    /// </summary>
    protected virtual CultureInfo ResolveCulture(string? cultureName)
    {
        if (TryGetCulture(cultureName, out var culture))
        {
            return culture;
        }

        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            Logger.LogWarning(
                "Recipient culture '{CultureName}' is not a valid culture name; falling back to '{DefaultCulture}'.",
                cultureName,
                EmailOptions.DefaultCulture);
        }

        if (TryGetCulture(EmailOptions.DefaultCulture, out var defaultCulture))
        {
            return defaultCulture;
        }

        // Also the invariant-globalization path, where no culture name resolves and there is nothing left to fall to.
        Logger.LogWarning(
            "{Options}.{Property} is '{DefaultCulture}', which is not a valid culture name; falling back to the "
            + "ambient culture '{AmbientCulture}'.",
            nameof(NotificationEmailOptions),
            nameof(NotificationEmailOptions.DefaultCulture),
            EmailOptions.DefaultCulture,
            CultureInfo.CurrentUICulture.Name);

        return CultureInfo.CurrentUICulture;
    }

    private static bool TryGetCulture(string? cultureName, [NotNullWhen(true)] out CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            culture = null;
            return false;
        }

        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }
}
