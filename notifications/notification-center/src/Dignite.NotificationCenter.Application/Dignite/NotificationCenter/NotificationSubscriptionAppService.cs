using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Localization;
using Volo.Abp.Users;

namespace Dignite.NotificationCenter;

[Authorize]
public class NotificationSubscriptionAppService : NotificationCenterAppService, INotificationSubscriptionAppService
{
    protected INotificationStore Store { get; }

    protected NotificationSubscriptionManager SubscriptionManager { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    public NotificationSubscriptionAppService(
        INotificationStore store,
        NotificationSubscriptionManager subscriptionManager,
        INotificationDefinitionManager definitionManager)
    {
        Store = store;
        SubscriptionManager = subscriptionManager;
        DefinitionManager = definitionManager;
    }

    public virtual async Task<ListResultDto<NotificationSubscriptionDto>> GetSubscriptionsAsync()
    {
        var userId = CurrentUser.GetId();

        var available = await DefinitionManager.GetAllAvailableAsync(userId);
        var subscribed = await Store.GetSubscriptionsAsync(userId);
        var availableByName = available.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
        var definitionWideSubscriptions = subscribed
            .Where(subscription => subscription.EntityTypeName == null && subscription.EntityId == null)
            .Select(subscription => subscription.NotificationName)
            .ToHashSet(StringComparer.Ordinal);

        var dtos = available.Select(definition => new NotificationSubscriptionDto
        {
            NotificationName = definition.Name,
            GroupName = definition.GroupName,
            GroupDisplayName = GetGroupDisplayName(DefinitionManager.GetGroupOrNull(definition.GroupName)),
            DisplayName = definition.DisplayName.Localize(StringLocalizerFactory).Value,
            Description = definition.Description?.Localize(StringLocalizerFactory)?.Value,
            IsSubscribed = definitionWideSubscriptions.Contains(definition.Name)
        }).ToList();

        foreach (var subscription in subscribed.Where(subscription =>
                     subscription.EntityTypeName != null || subscription.EntityId != null))
        {
            availableByName.TryGetValue(subscription.NotificationName, out var definition);
            var group = GetGroupOrNull(subscription.NotificationName);
            dtos.Add(new NotificationSubscriptionDto
            {
                NotificationName = subscription.NotificationName,
                GroupName = group?.Name ?? NotificationCenterConsts.OtherGroupName,
                GroupDisplayName = GetGroupDisplayName(group),
                EntityTypeName = subscription.EntityTypeName,
                EntityId = subscription.EntityId,
                DisplayName = definition?.DisplayName.Localize(StringLocalizerFactory).Value,
                Description = definition?.Description?.Localize(StringLocalizerFactory)?.Value,
                IsSubscribed = true
            });
        }

        foreach (var subscription in subscribed.Where(subscription =>
                     subscription.EntityTypeName == null && subscription.EntityId == null
                     && !availableByName.ContainsKey(subscription.NotificationName)))
        {
            var group = GetGroupOrNull(subscription.NotificationName);
            dtos.Add(new NotificationSubscriptionDto
            {
                NotificationName = subscription.NotificationName,
                GroupName = group?.Name ?? NotificationCenterConsts.OtherGroupName,
                GroupDisplayName = GetGroupDisplayName(group),
                IsSubscribed = true
            });
        }

        return new ListResultDto<NotificationSubscriptionDto>(dtos);
    }

    public virtual Task SubscribeAsync(NotificationSubscriptionScopeDto input)
    {
        return SubscriptionManager.SubscribeAsync(
            CurrentUser.GetId(), input.NotificationName, CreateEntityIdentifier(input));
    }

    public virtual Task UnsubscribeAsync(NotificationSubscriptionScopeDto input)
    {
        return SubscriptionManager.UnsubscribeAsync(
            CurrentUser.GetId(), input.NotificationName, CreateEntityIdentifier(input));
    }

    /// <summary>The group of a defined notification (available to the user or not), or null when it no longer exists.</summary>
    protected virtual NotificationGroupDefinition? GetGroupOrNull(string notificationName)
    {
        var definition = DefinitionManager.GetOrNull(notificationName);
        return definition == null ? null : DefinitionManager.GetGroupOrNull(definition.GroupName);
    }

    protected virtual NotificationEntityIdentifier? CreateEntityIdentifier(NotificationSubscriptionScopeDto input)
    {
        if (input.EntityTypeName == null && input.EntityId == null)
        {
            return null;
        }

        if (input.EntityTypeName == null || input.EntityId == null)
        {
            throw new ArgumentException("EntityTypeName and EntityId must either both be supplied or both be null.", nameof(input));
        }

        return new NotificationEntityIdentifier(input.EntityTypeName, input.EntityId);
    }
}
