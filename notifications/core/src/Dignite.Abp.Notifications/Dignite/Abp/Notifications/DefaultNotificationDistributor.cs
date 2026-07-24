using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications;

public class DefaultNotificationDistributor :
    INotificationDistributor,
    ITransientDependency
{
    protected INotificationStore Store { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    protected IDistributedEventBus DistributedEventBus { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected ILogger<DefaultNotificationDistributor> Logger { get; }

    protected NotificationDistributionOptions Options { get; }

    public DefaultNotificationDistributor(
        INotificationStore store,
        INotificationDefinitionManager definitionManager,
        IDistributedEventBus distributedEventBus,
        INotificationDataSerializer dataSerializer,
        ICurrentTenant currentTenant,
        ILogger<DefaultNotificationDistributor> logger,
        IOptions<NotificationDistributionOptions> options)
    {
        Store = store;
        DefinitionManager = definitionManager;
        DistributedEventBus = distributedEventBus;
        DataSerializer = dataSerializer;
        CurrentTenant = currentTenant;
        Logger = logger;
        Options = options.Value;
        Options.Validate();
    }

    public virtual async Task DistributeAsync(
        NotificationInfo notification,
        Guid[]? userIds = null,
        Guid[]? excludedUserIds = null,
        CancellationToken cancellationToken = default)
    {
        // An empty, explicitly supplied recipient list is intentionally different from null. Return before
        // subscription lookup or channel validation so every direct/background path remains a true no-op.
        if (userIds is { Length: 0 })
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var state = new DistributionState();

        try
        {
            // Notification.TenantId is authoritative for the whole operation; null means host and never falls
            // back to the caller's ambient tenant.
            using (CurrentTenant.Change(notification.TenantId, null))
            {
                var channels = ResolveExternalChannelsOrNull(notification.NotificationName);
                var excluded = excludedUserIds is { Length: > 0 } ? new HashSet<Guid>(excludedUserIds) : null;

                if (userIds != null)
                {
                    var candidates = userIds.Distinct().Where(id => excluded == null || !excluded.Contains(id));
                    foreach (var batch in candidates.Chunk(Options.RecipientBatchSize))
                    {
                        await ProcessCandidateBatchAsync(notification, batch, channels, state, cancellationToken);
                    }
                }
                else
                {
                    Guid? afterUserId = null;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var page = await Store.GetSubscriptionUserIdsAsync(
                            notification.NotificationName,
                            notification.EntityTypeName,
                            notification.EntityId,
                            afterUserId,
                            Options.RecipientBatchSize,
                            cancellationToken);
                        if (page.Count == 0)
                        {
                            break;
                        }

                        afterUserId = page[^1];
                        var batch = excluded == null
                            ? page.ToArray()
                            : page.Where(id => !excluded.Contains(id)).ToArray();
                        await ProcessCandidateBatchAsync(notification, batch, channels, state, cancellationToken);

                        if (page.Count < Options.RecipientBatchSize)
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation(
                "Notification distribution for '{NotificationName}' ({NotificationId}) was canceled after " +
                "{RecipientCount} recipients.",
                notification.NotificationName,
                notification.Id,
                state.RecipientCount);
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Notification distribution for '{NotificationName}' ({NotificationId}) failed after " +
                "{RecipientCount} recipients.",
                notification.NotificationName,
                notification.Id,
                state.RecipientCount);
            throw;
        }

        Logger.LogInformation(
            "Notification '{NotificationName}' ({NotificationId}) distributed to {RecipientCount} recipients " +
            "({FilteredCount} filtered by definition requirements).",
            notification.NotificationName,
            notification.Id,
            state.RecipientCount,
            state.FilteredCount);
    }

    protected virtual async Task ProcessCandidateBatchAsync(
        NotificationInfo notification,
        IReadOnlyList<Guid> candidates,
        string[]? channels,
        DistributionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Definition permission/feature requirements gate delivery for explicit and subscription-derived
        // candidates alike; an explicit userIds array is not an authorization bypass.
        var eligible = new List<Guid>(candidates.Count);
        foreach (var userId in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DefinitionManager.IsAvailableAsync(notification.NotificationName, userId))
            {
                eligible.Add(userId);
            }
            else
            {
                state.FilteredCount++;
            }
        }

        if (eligible.Count == 0)
        {
            return;
        }

        if (!state.NotificationInserted)
        {
            await Store.InsertNotificationAsync(notification, cancellationToken);
            state.NotificationInserted = true;
        }

        var inboxRows = eligible.Select(userId => new UserNotificationInfo
        {
            UserId = userId,
            NotificationId = notification.Id,
            State = UserNotificationState.Unread,
            CreationTime = notification.CreationTime,
            TenantId = notification.TenantId
        }).ToList();

        await Store.InsertUserNotificationsAsync(inboxRows, cancellationToken);
        state.RecipientCount += eligible.Count;

        if (channels == null)
        {
            return;
        }

        foreach (var userId in eligible)
        {
            foreach (var channel in channels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DistributedEventBus.PublishAsync(CreateDeliveryRequest(notification, userId, channel));
            }
        }
    }

    protected virtual string[]? ResolveExternalChannelsOrNull(string notificationName)
    {
        var definition = DefinitionManager.Get(notificationName);

        var channels = definition.GetChannelsOrNull();
        if (channels == null)
        {
            if (Store is NullNotificationStore)
            {
                throw new AbpException(
                    $"Notification '{notificationName}' has no external channels and no NotificationCenter inbox store is installed. Configure UseChannels(...) or install NotificationCenter.");
            }

            return null;
        }

        if (channels.Length == 0 || channels.Any(string.IsNullOrWhiteSpace))
        {
            throw new AbpException(
                $"Notification '{notificationName}' has invalid delivery channel configuration.");
        }

        return channels.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    protected virtual NotificationDeliveryRequestedEto CreateDeliveryRequest(
        NotificationInfo notification,
        Guid userId,
        string channel)
    {
        // The ETO must stay a flat, default-STJ-serializable POCO (ABP's outbox/inbox serializes it without the
        // app's JSON options), so the polymorphic payload crosses the wire as a discriminator-tagged string.
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = notification.Id,
            NotificationName = notification.NotificationName,
            DataJson = DataSerializer.Serialize(notification.Data),
            Severity = notification.Severity,
            CreationTime = notification.CreationTime,
            UserId = userId,
            Channel = channel,
            TenantId = notification.TenantId,
            EntityTypeName = notification.EntityTypeName,
            EntityId = notification.EntityId
        };
    }

    protected sealed class DistributionState
    {
        public int RecipientCount { get; set; }

        public int FilteredCount { get; set; }

        public bool NotificationInserted { get; set; }
    }
}
