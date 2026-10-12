using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Stands in for the definition store in a test: definitions "saved by another process", which a test adds when it
/// wants them to have arrived.
/// </summary>
[DisableConventionalRegistration]
public class TestDynamicNotificationDefinitionStore : IDynamicNotificationDefinitionStore
{
    private readonly NotificationDefinitionContext _context = new();

    public void Define(Action<INotificationDefinitionContext> define)
    {
        define(_context);
    }

    public Task<NotificationDefinition?> GetOrNullAsync(string name)
    {
        return Task.FromResult(_context.GetOrNull(name));
    }

    public Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync()
    {
        return Task.FromResult<IReadOnlyList<NotificationDefinition>>(
            _context.Groups.SelectMany(group => group.Notifications).ToList());
    }

    public Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync()
    {
        return Task.FromResult<IReadOnlyList<NotificationGroupDefinition>>(_context.Groups.ToList());
    }
}
