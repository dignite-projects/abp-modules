using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Dignite.NotificationCenter;

/// <summary>
/// EF-backed implementation of the core <see cref="INotificationStore"/>. Replaces the framework's
/// <c>NullNotificationStore</c> when this module is installed. Both write and read go through the single core
/// <see cref="INotificationDataSerializer"/> (System.Text.Json + stable discriminator) — no Newtonsoft, no
/// AssemblyQualifiedName. Durable reads use its additive tolerant-reader capability so one historical payload
/// cannot fail a whole inbox page. Queries use proper joins/indexes (fixes roadmap problem D).
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(INotificationStore))]
public class NotificationStore : INotificationStore, ITransientDependency
{
    protected IRepository<Notification, Guid> NotificationRepository { get; }

    protected IRepository<UserNotification, Guid> UserNotificationRepository { get; }

    protected IRepository<NotificationSubscription, Guid> SubscriptionRepository { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected IClock Clock { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected IAsyncQueryableExecuter AsyncExecuter { get; }

    public NotificationStore(
        IRepository<Notification, Guid> notificationRepository,
        IRepository<UserNotification, Guid> userNotificationRepository,
        IRepository<NotificationSubscription, Guid> subscriptionRepository,
        INotificationDataSerializer dataSerializer,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant,
        IAsyncQueryableExecuter asyncExecuter)
    {
        NotificationRepository = notificationRepository;
        UserNotificationRepository = userNotificationRepository;
        SubscriptionRepository = subscriptionRepository;
        DataSerializer = dataSerializer;
        GuidGenerator = guidGenerator;
        Clock = clock;
        CurrentTenant = currentTenant;
        AsyncExecuter = asyncExecuter;
    }

    public virtual async Task InsertNotificationAsync(
        NotificationInfo notification,
        CancellationToken cancellationToken = default)
    {
        var entity = new Notification(
            notification.Id,
            notification.NotificationName,
            DataSerializer.Serialize(notification.Data),
            notification.EntityTypeName,
            notification.EntityId,
            notification.Severity,
            notification.CreationTime,
            notification.TenantId ?? CurrentTenant.Id);

        await NotificationRepository.InsertAsync(entity, cancellationToken: cancellationToken);
    }

    public virtual async Task InsertUserNotificationAsync(
        UserNotificationInfo userNotification,
        CancellationToken cancellationToken = default)
    {
        await InsertUserNotificationsAsync(new[] { userNotification }, cancellationToken);
    }

    public virtual async Task InsertUserNotificationsAsync(
        IReadOnlyCollection<UserNotificationInfo> userNotifications,
        CancellationToken cancellationToken = default)
    {
        if (userNotifications.Count == 0)
        {
            return;
        }

        var deduplicated = userNotifications
            .GroupBy(userNotification => new { userNotification.UserId, userNotification.NotificationId })
            .Select(group => group.First())
            .ToList();

        // One pre-check keeps a re-run of the same distribution (e.g. a retried background job) from
        // violating the unique (UserId, NotificationId) inbox index.
        var existingKeys = await GetExistingUserNotificationKeysAsync(deduplicated, cancellationToken);
        var entities = deduplicated
            .Where(userNotification => !existingKeys.Contains((
                userNotification.UserId,
                userNotification.NotificationId)))
            .Select(userNotification => new UserNotification(
                userNotification.Id == Guid.Empty ? GuidGenerator.Create() : userNotification.Id,
                userNotification.UserId,
                userNotification.NotificationId,
                userNotification.State,
                userNotification.CreationTime == default ? Clock.Now : userNotification.CreationTime,
                userNotification.TenantId ?? CurrentTenant.Id))
            .ToList();
        if (entities.Count == 0)
        {
            return;
        }

        await UserNotificationRepository.InsertManyAsync(
            entities,
            autoSave: true,
            cancellationToken: cancellationToken);
    }

    protected virtual async Task<HashSet<(Guid UserId, Guid NotificationId)>> GetExistingUserNotificationKeysAsync(
        IReadOnlyCollection<UserNotificationInfo> userNotifications,
        CancellationToken cancellationToken)
    {
        var userIds = userNotifications.Select(userNotification => userNotification.UserId).Distinct().ToList();
        var notificationIds = userNotifications
            .Select(userNotification => userNotification.NotificationId)
            .Distinct()
            .ToList();

        var query = await UserNotificationRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            query
                .Where(row => userIds.Contains(row.UserId) && notificationIds.Contains(row.NotificationId))
                .Select(row => new { row.UserId, row.NotificationId }),
            cancellationToken);
        return rows
            .Select(row => (row.UserId, row.NotificationId))
            .ToHashSet();
    }

    public virtual async Task UpdateUserNotificationStateAsync(
        Guid userId,
        Guid notificationId,
        UserNotificationState state,
        CancellationToken cancellationToken = default)
    {
        var entity = await UserNotificationRepository.FirstOrDefaultAsync(
            x => x.UserId == userId && x.NotificationId == notificationId,
            cancellationToken: cancellationToken);

        if (entity != null)
        {
            entity.SetState(state);
            await UserNotificationRepository.UpdateAsync(entity, cancellationToken: cancellationToken);
        }
    }

    public virtual async Task UpdateAllUserNotificationStatesAsync(
        Guid userId,
        UserNotificationState state,
        CancellationToken cancellationToken = default)
    {
        var entities = await UserNotificationRepository.GetListAsync(
            x => x.UserId == userId,
            cancellationToken: cancellationToken);
        foreach (var entity in entities)
        {
            entity.SetState(state);
        }

        await UserNotificationRepository.UpdateManyAsync(entities, cancellationToken: cancellationToken);
    }

    public virtual async Task DeleteUserNotificationAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        await UserNotificationRepository.DeleteAsync(
            x => x.UserId == userId && x.NotificationId == notificationId,
            cancellationToken: cancellationToken);
    }

    public virtual async Task DeleteAllUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        await UserNotificationRepository.DeleteAsync(x =>
            x.UserId == userId
            && (state == null || x.State == state)
            && (startDate == null || x.CreationTime >= startDate)
            && (endDate == null || x.CreationTime <= endDate),
            cancellationToken: cancellationToken);
    }

    public virtual async Task<List<UserNotificationWithNotification>> GetUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // Two indexed queries + an in-memory join, rather than a cross-collection join, so the SAME store works on
        // both EF Core and MongoDB. The (UserId, State, CreationTime) index serves the first query; the second is a
        // primary-key batch lookup. A user-notification whose notification was deleted is skipped, not thrown on
        // (roadmap problem D).
        var userNotificationQuery = await UserNotificationRepository.GetQueryableAsync();
        userNotificationQuery = userNotificationQuery.Where(un =>
            un.UserId == userId
            && (state == null || un.State == state)
            && (startDate == null || un.CreationTime >= startDate)
            && (endDate == null || un.CreationTime <= endDate));

        var pagedUserNotifications = await AsyncExecuter.ToListAsync(
            userNotificationQuery
                .OrderByDescending(un => un.CreationTime)
                .Skip(skipCount)
                .Take(maxResultCount),
            cancellationToken);

        if (pagedUserNotifications.Count == 0)
        {
            return new List<UserNotificationWithNotification>();
        }

        var notificationIds = pagedUserNotifications.Select(un => un.NotificationId).Distinct().ToList();
        var notifications = await NotificationRepository.GetListAsync(
            n => notificationIds.Contains(n.Id),
            cancellationToken: cancellationToken);
        var notificationsById = notifications.ToDictionary(n => n.Id);

        var result = new List<UserNotificationWithNotification>();
        foreach (var userNotification in pagedUserNotifications)
        {
            if (notificationsById.TryGetValue(userNotification.NotificationId, out var notification))
            {
                result.Add(new UserNotificationWithNotification
                {
                    UserNotification = MapToUserNotificationInfo(userNotification),
                    Notification = MapToNotificationInfo(notification)
                });
            }
        }

        return result;
    }

    public virtual async Task<int> GetUserNotificationCountAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = await UserNotificationRepository.GetQueryableAsync();
        query = query.Where(un =>
            un.UserId == userId
            && (state == null || un.State == state)
            && (startDate == null || un.CreationTime >= startDate)
            && (endDate == null || un.CreationTime <= endDate));

        return await AsyncExecuter.CountAsync(query, cancellationToken);
    }

    public virtual async Task InsertSubscriptionAsync(
        NotificationSubscriptionInfo subscription,
        CancellationToken cancellationToken = default)
    {
        var entity = new NotificationSubscription(
            GuidGenerator.Create(),
            subscription.UserId,
            subscription.NotificationName,
            subscription.EntityTypeName,
            subscription.EntityId,
            subscription.CreationTime == default ? Clock.Now : subscription.CreationTime,
            subscription.TenantId ?? CurrentTenant.Id);

        await SubscriptionRepository.InsertAsync(entity, cancellationToken: cancellationToken);
    }

    public virtual async Task DeleteSubscriptionAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = NotificationSubscriptionIdentity.GetTenantKey(CurrentTenant.Id);
        var notificationNameKey = NotificationSubscriptionIdentity.GetNotificationNameKey(notificationName);
        var scopeKey = NotificationSubscriptionIdentity.GetScopeKey(entityTypeName, entityId);

        await SubscriptionRepository.DeleteAsync(x =>
            x.TenantKey == tenantKey && x.UserId == userId
            && x.NotificationNameKey == notificationNameKey && x.ScopeKey == scopeKey,
            cancellationToken: cancellationToken);
    }

    public virtual async Task<bool> IsSubscribedAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = NotificationSubscriptionIdentity.GetTenantKey(CurrentTenant.Id);
        var notificationNameKey = NotificationSubscriptionIdentity.GetNotificationNameKey(notificationName);
        var scopeKey = NotificationSubscriptionIdentity.GetScopeKey(entityTypeName, entityId);
        var query = await SubscriptionRepository.GetQueryableAsync();
        return await AsyncExecuter.AnyAsync(query.Where(x =>
            x.TenantKey == tenantKey && x.UserId == userId
            && x.NotificationNameKey == notificationNameKey && x.ScopeKey == scopeKey), cancellationToken);
    }

    public virtual async Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = NotificationSubscriptionIdentity.GetTenantKey(CurrentTenant.Id);
        var notificationNameKey = NotificationSubscriptionIdentity.GetNotificationNameKey(notificationName);
        var requestedScopeKey = NotificationSubscriptionIdentity.GetScopeKey(entityTypeName, entityId);
        var definitionWideScopeKey = NotificationSubscriptionIdentity.GetScopeKey(null, null);

        var entities = entityTypeName == null
            ? await SubscriptionRepository.GetListAsync(x =>
                x.TenantKey == tenantKey && x.NotificationNameKey == notificationNameKey
                && x.ScopeKey == definitionWideScopeKey,
                cancellationToken: cancellationToken)
            : await SubscriptionRepository.GetListAsync(x =>
                x.TenantKey == tenantKey && x.NotificationNameKey == notificationNameKey
                && (x.ScopeKey == definitionWideScopeKey || x.ScopeKey == requestedScopeKey),
                cancellationToken: cancellationToken);

        return entities.Select(MapToSubscriptionInfo).ToList();
    }

    public virtual async Task<List<Guid>> GetSubscriptionUserIdsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        Guid? afterUserId,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResultCount);

        var tenantKey = NotificationSubscriptionIdentity.GetTenantKey(CurrentTenant.Id);
        var notificationNameKey = NotificationSubscriptionIdentity.GetNotificationNameKey(notificationName);
        var requestedScopeKey = NotificationSubscriptionIdentity.GetScopeKey(entityTypeName, entityId);
        var definitionWideScopeKey = NotificationSubscriptionIdentity.GetScopeKey(null, null);
        var query = await SubscriptionRepository.GetQueryableAsync();
        query = entityTypeName == null
            ? query.Where(subscription =>
                subscription.TenantKey == tenantKey &&
                subscription.NotificationNameKey == notificationNameKey &&
                subscription.ScopeKey == definitionWideScopeKey)
            : query.Where(subscription =>
                subscription.TenantKey == tenantKey &&
                subscription.NotificationNameKey == notificationNameKey &&
                (subscription.ScopeKey == definitionWideScopeKey ||
                 subscription.ScopeKey == requestedScopeKey));

        var recipientQuery = query.Select(subscription => subscription.UserId).Distinct();
        if (afterUserId.HasValue)
        {
            var cursor = afterUserId.Value;
            recipientQuery = recipientQuery.Where(userId => userId.CompareTo(cursor) > 0);
        }

        var recipientPage = recipientQuery
            .OrderBy(userId => userId)
            .Take(maxResultCount);

        return await AsyncExecuter.ToListAsync(recipientPage, cancellationToken);
    }

    public virtual async Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = NotificationSubscriptionIdentity.GetTenantKey(CurrentTenant.Id);
        var entities = await SubscriptionRepository.GetListAsync(x =>
            x.TenantKey == tenantKey && x.UserId == userId,
            cancellationToken: cancellationToken);
        return entities.Select(MapToSubscriptionInfo).ToList();
    }

    protected virtual NotificationInfo MapToNotificationInfo(Notification n)
    {
        return new NotificationInfo
        {
            Id = n.Id,
            NotificationName = n.NotificationName,
            Data = DeserializeDurableData(n.Data),
            EntityTypeName = n.EntityTypeName,
            EntityId = n.EntityId,
            Severity = n.Severity,
            CreationTime = n.CreationTime,
            TenantId = n.TenantId
        };
    }

    protected virtual UserNotificationInfo MapToUserNotificationInfo(UserNotification un)
    {
        return new UserNotificationInfo
        {
            Id = un.Id,
            UserId = un.UserId,
            NotificationId = un.NotificationId,
            State = un.State,
            CreationTime = un.CreationTime,
            TenantId = un.TenantId
        };
    }

    protected virtual NotificationData? DeserializeDurableData(string? json)
    {
        return DataSerializer.Deserialize(json);
    }

    protected virtual NotificationSubscriptionInfo MapToSubscriptionInfo(NotificationSubscription s)
    {
        return new NotificationSubscriptionInfo
        {
            UserId = s.UserId,
            NotificationName = s.NotificationName,
            EntityTypeName = s.EntityTypeName,
            EntityId = s.EntityId,
            CreationTime = s.CreationTime,
            TenantId = s.TenantId
        };
    }
}
