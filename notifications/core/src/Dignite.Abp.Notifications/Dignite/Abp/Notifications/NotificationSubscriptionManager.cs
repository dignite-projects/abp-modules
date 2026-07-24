using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Applies notification-definition availability and exact-scope identity rules while mutating subscriptions.
/// Subscription reads belong to the consuming application/query path through <see cref="INotificationStore"/>.
/// </summary>
public class NotificationSubscriptionManager : ITransientDependency
{
    protected INotificationStore Store { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    protected IClock Clock { get; }

    public NotificationSubscriptionManager(
        INotificationStore store,
        INotificationDefinitionManager definitionManager,
        IClock clock)
    {
        Store = store;
        DefinitionManager = definitionManager;
        Clock = clock;
    }

    public virtual async Task SubscribeAsync(
        Guid userId, string notificationName, NotificationEntityIdentifier? entityIdentifier = null)
    {
        var (entityTypeName, entityId) = Deconstruct(entityIdentifier);

        if (!await DefinitionManager.IsAvailableAsync(notificationName, userId))
        {
            return;
        }

        await SubscribeIfNotSubscribedAsync(userId, notificationName, entityTypeName, entityId);
    }

    protected virtual async Task SubscribeIfNotSubscribedAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId)
    {
        if (await Store.IsSubscribedAsync(userId, notificationName, entityTypeName, entityId))
        {
            return;
        }

        await Store.InsertSubscriptionAsync(new NotificationSubscriptionInfo
        {
            UserId = userId,
            NotificationName = notificationName,
            EntityTypeName = entityTypeName,
            EntityId = entityId,
            CreationTime = Clock.Now
        });
    }

    public virtual async Task SubscribeToAllAvailableNotificationsAsync(Guid userId)
    {
        var definitions = await DefinitionManager.GetAllAvailableAsync(userId);
        foreach (var definition in definitions)
        {
            await SubscribeIfNotSubscribedAsync(userId, definition.Name, null, null);
        }
    }

    public virtual Task UnsubscribeAsync(
        Guid userId, string notificationName, NotificationEntityIdentifier? entityIdentifier = null)
    {
        var (entityTypeName, entityId) = Deconstruct(entityIdentifier);
        return Store.DeleteSubscriptionAsync(userId, notificationName, entityTypeName, entityId);
    }

    protected static (string? EntityTypeName, string? EntityId) Deconstruct(NotificationEntityIdentifier? identifier)
    {
        return identifier == null
            ? (null, null)
            : (identifier.EntityTypeName, identifier.EntityId);
    }
}
