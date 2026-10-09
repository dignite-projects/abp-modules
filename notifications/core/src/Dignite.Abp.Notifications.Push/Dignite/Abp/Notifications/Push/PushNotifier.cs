using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// Relays notifications to the recipient's phones. Like email, a device push needs a UserId → address mapping — here
/// the user's registered devices, supplied by <see cref="IPushDeviceStore"/>. Each device names the
/// <see cref="IPushProvider"/> that issued its token, so one channel spans Expo, FCM, APNs, ... Which notifications reach it is
/// decided by <see cref="NotificationRoutingOptions"/>.
/// </summary>
/// <remarks>
/// Delivery is best-effort: no retry and no delivery state. Content is built once per device culture. A device a
/// provider reports dead is removed from the store; one provider failing — or failing to have its dead devices
/// removed — does not stop the others. Each failure is logged here with its provider (exception type only, never the
/// message, like the delivery handler), and rethrown once every provider has been tried so the delivery counts as failed.
/// <para>
/// The constructor never throws: a provider-name clash fails each push delivery instead, through the same best-effort
/// path as any other delivery failure, rather than escaping the delivery handler while it resolves the notifier.
/// (That handler builds only the notifier of the event's channel, so other channels are unaffected either way.)
/// </para>
/// </remarks>
[ExposeServices(
    typeof(INotificationNotifier),
    typeof(PushNotifier))]
public class PushNotifier :
    INotificationNotifier,
    ITransientDependency
{
    public const string ChannelName = "Push";

    public string Name => ChannelName;

    protected IPushDeviceStore DeviceStore { get; }

    protected IReadOnlyDictionary<string, IPushProvider> Providers { get; }

    /// <summary>
    /// Set when two registered providers claim the same name; every push delivery then fails with this message.
    /// </summary>
    protected string? ProviderConfigurationError { get; }

    protected INotificationPushBuilder PushBuilder { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    protected ILogger<PushNotifier> Logger { get; }

    protected NotificationPushOptions PushOptions { get; }

    public PushNotifier(
        IPushDeviceStore deviceStore,
        IEnumerable<IPushProvider> providers,
        INotificationPushBuilder pushBuilder,
        INotificationDataSerializer dataSerializer,
        ILogger<PushNotifier> logger,
        IOptions<NotificationPushOptions> pushOptions)
    {
        DeviceStore = deviceStore;
        PushBuilder = pushBuilder;
        DataSerializer = dataSerializer;
        Logger = logger;
        PushOptions = pushOptions.Value;

        var byName = new Dictionary<string, IPushProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (byName.TryGetValue(provider.Name, out var existing))
            {
                ProviderConfigurationError ??=
                    $"Push providers '{existing.GetType().FullName}' and '{provider.GetType().FullName}' both claim the "
                    + $"name '{provider.Name}'. Each {nameof(IPushProvider)} needs a distinct name.";
                continue;
            }

            byName[provider.Name] = provider;
        }

        Providers = byName;
    }

    public virtual async Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Channel, Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {nameof(PushNotifier)} cannot deliver channel '{request.Channel}'.");
        }

        if (ProviderConfigurationError != null)
        {
            throw new InvalidOperationException(ProviderConfigurationError);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var notification = NotificationPayload.FromRequest(request, DataSerializer);

        var targets = await DeviceStore.GetTargetsAsync(request.UserId, cancellationToken);
        if (targets.Count == 0)
        {
            Logger.LogDebug(
                "No push device registered for the recipient of notification '{NotificationName}'; skipping push delivery.",
                notification.NotificationName);
            return;
        }

        var messages = await BuildMessagesAsync(notification, request.UserId, request.TenantId, targets, cancellationToken);
        if (messages.Count == 0)
        {
            return;
        }

        await SendAsync(notification, messages, cancellationToken);
    }

    /// <summary>Builds the content once per device culture and pairs it with every device in that culture.</summary>
    protected virtual async Task<List<(PushTarget Target, PushMessage Message)>> BuildMessagesAsync(
        NotificationPayload notification,
        Guid userId,
        Guid? tenantId,
        IReadOnlyList<PushTarget> targets,
        CancellationToken cancellationToken)
    {
        var data = BuildData(notification);
        var messages = new List<(PushTarget Target, PushMessage Message)>();

        foreach (var cultureGroup in targets.GroupBy(target => target.CultureName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var culture = ResolveCulture(cultureGroup.Key);
            NotificationPushContent? content;

            // CultureInfo is backed by AsyncLocal. Set it only around this culture's content build; CultureHelper.Use
            // restores both values so the next group, or another delivery, cannot inherit it.
            using (CultureHelper.Use(culture))
            {
                content = await PushBuilder.BuildAsync(
                    new NotificationPushBuildContext(notification, userId, tenantId, culture.Name),
                    cancellationToken);
            }

            if (content == null)
            {
                Logger.LogDebug(
                    "No push content provider produced content for notification '{NotificationName}' in culture "
                    + "'{CultureName}'.",
                    notification.NotificationName,
                    culture.Name);
                continue;
            }

            foreach (var target in cultureGroup)
            {
                messages.Add((target, new PushMessage(target.Token, content.Title, content.Body, data)));
            }
        }

        return messages;
    }

    /// <summary>The silent payload a tapped notification hands to the app. See <see cref="PushDataKeys"/>.</summary>
    protected virtual IReadOnlyDictionary<string, string> BuildData(NotificationPayload notification)
    {
        var data = new Dictionary<string, string>
        {
            [PushDataKeys.NotificationId] = notification.NotificationId.ToString(),
            [PushDataKeys.NotificationName] = notification.NotificationName
        };

        if (notification.EntityTypeName != null)
        {
            data[PushDataKeys.EntityTypeName] = notification.EntityTypeName;
        }

        if (notification.EntityId != null)
        {
            data[PushDataKeys.EntityId] = notification.EntityId;
        }

        return data;
    }

    /// <summary>Hands each provider its devices' messages and acts on what it reports.</summary>
    protected virtual async Task SendAsync(
        NotificationPayload notification,
        List<(PushTarget Target, PushMessage Message)> messages,
        CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        foreach (var providerGroup in messages.GroupBy(item => item.Target.Provider, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = providerGroup.ToList();

            if (!Providers.TryGetValue(providerGroup.Key, out var provider))
            {
                Logger.LogWarning(
                    "No push provider named '{Provider}' is registered; skipping {DeviceCount} device(s) for "
                    + "notification '{NotificationName}'.",
                    providerGroup.Key,
                    items.Count,
                    notification.NotificationName);
                continue;
            }

            try
            {
                var results = await provider.SendAsync(items.Select(item => item.Message).ToList(), cancellationToken);
                await HandleResultsAsync(notification, provider.Name, items, results, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Exception type only: a provider's message may echo request details, and the delivery handler keeps
                // the same rule. The provider name is what an operator needs to find the failing integration.
                Logger.LogWarning(
                    "Push provider '{Provider}' failed with {ExceptionType} while delivering to {DeviceCount} "
                    + "device(s) for notification '{NotificationName}' ({NotificationId}).",
                    provider.Name,
                    exception.GetType().FullName,
                    items.Count,
                    notification.NotificationName,
                    notification.NotificationId);
                failures.Add(exception);
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(
                $"{failures.Count} push providers failed to deliver notification '{notification.NotificationName}'.",
                failures);
        }
    }

    /// <summary>
    /// Removes the devices the provider reported dead — under the provider name each device was stored with rather
    /// than the provider's own spelling, so a store matching names exactly still finds them — and logs one summary
    /// line for rejected messages. Never logs tokens.
    /// </summary>
    protected virtual async Task HandleResultsAsync(
        NotificationPayload notification,
        string providerName,
        IReadOnlyList<(PushTarget Target, PushMessage Message)> sent,
        IReadOnlyList<PushSendResult> results,
        CancellationToken cancellationToken)
    {
        var deadTokens = results
            .Where(result => result.Status == PushSendStatus.TokenInvalid)
            .Select(result => result.Token)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var target in sent.Select(item => item.Target).Where(target => deadTokens.Contains(target.Token)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DeviceStore.RemoveAsync(target.Provider, target.Token, cancellationToken);
        }

        var failed = results.Where(result => result.Status == PushSendStatus.Failed).ToList();
        if (failed.Count > 0)
        {
            Logger.LogWarning(
                "Push provider '{Provider}' rejected {FailedCount} of {MessageCount} message(s) for notification "
                + "'{NotificationName}': {Errors}.",
                providerName,
                failed.Count,
                results.Count,
                notification.NotificationName,
                string.Join(", ", failed.Select(result => result.Error ?? "unknown").Distinct(StringComparer.Ordinal)));
        }
    }

    /// <summary>
    /// Falls back rather than throws; see <see cref="NotificationCultureResolver"/>. A device registered with a bad
    /// culture name should get the default culture's text, not lose the push.
    /// </summary>
    protected virtual CultureInfo ResolveCulture(string? cultureName)
    {
        return NotificationCultureResolver.Resolve(
            cultureName,
            PushOptions.DefaultCulture,
            $"{nameof(NotificationPushOptions)}.{nameof(NotificationPushOptions.DefaultCulture)}",
            Logger);
    }
}
