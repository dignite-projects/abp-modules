using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

public class NotificationDefinitionContext : INotificationDefinitionContext
{
    /// <summary>Groups in registration order.</summary>
    public IReadOnlyList<NotificationGroupDefinition> Groups => _groups;
    private readonly List<NotificationGroupDefinition> _groups;

    internal Dictionary<string, NotificationDefinition> Definitions { get; }

    private readonly Dictionary<string, Type?> _groupProviders;

    private readonly Dictionary<string, Type?> _definitionProviders;

    private Type? _currentProviderType;

    public NotificationDefinitionContext()
    {
        _groups = new List<NotificationGroupDefinition>();
        Definitions = new Dictionary<string, NotificationDefinition>(StringComparer.Ordinal);
        _groupProviders = new Dictionary<string, Type?>(StringComparer.Ordinal);
        _definitionProviders = new Dictionary<string, Type?>(StringComparer.Ordinal);
    }

    public NotificationGroupDefinition AddGroup(string name, ILocalizableString? displayName = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        if (_groupProviders.TryGetValue(name, out var existingProvider))
        {
            throw new InvalidOperationException(
                $"Notification group name '{name}' is registered by conflicting providers " +
                $"{FormatProviderPair(existingProvider, _currentProviderType)}. Group names use ordinal, " +
                "case-sensitive comparison; use GetGroupOrNull to add definitions to an existing group.");
        }

        var group = new NotificationGroupDefinition(this, name, displayName);
        _groups.Add(group);
        _groupProviders.Add(name, _currentProviderType);
        return group;
    }

    public NotificationGroupDefinition? GetGroupOrNull(string name)
    {
        return _groups.FirstOrDefault(group => group.Name == name);
    }

    public NotificationDefinition? GetOrNull(string name)
    {
        return Definitions.TryGetValue(name, out var definition) ? definition : null;
    }

    internal void AddDefinition(NotificationDefinition definition)
    {
        Check.NotNull(definition, nameof(definition));

        if (_definitionProviders.TryGetValue(definition.Name, out var existingProvider))
        {
            throw new InvalidOperationException(
                $"Notification definition name '{definition.Name}' is registered by conflicting providers " +
                $"{FormatProviderPair(existingProvider, _currentProviderType)}. Definition names use ordinal, " +
                "case-sensitive comparison and are unique across all groups.");
        }

        Definitions.Add(definition.Name, definition);
        _definitionProviders.Add(definition.Name, _currentProviderType);
    }

    internal void SetCurrentProvider(Type providerType)
    {
        _currentProviderType = Check.NotNull(providerType, nameof(providerType));
    }

    private static string FormatProviderPair(Type? first, Type? second)
    {
        var providerNames = new[] { GetProviderName(first), GetProviderName(second) }
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return $"'{providerNames[0]}' and '{providerNames[1]}'";
    }

    private static string GetProviderName(Type? providerType)
    {
        if (providerType == null)
        {
            return "<direct registration>";
        }

        var assemblyName = providerType.Assembly.GetName().Name ?? "<unknown assembly>";
        return $"{providerType.FullName ?? providerType.Name}, {assemblyName}";
    }
}
