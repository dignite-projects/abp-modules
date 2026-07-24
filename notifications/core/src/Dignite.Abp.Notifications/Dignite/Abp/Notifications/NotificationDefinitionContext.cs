using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

public class NotificationDefinitionContext : INotificationDefinitionContext
{
    internal Dictionary<string, NotificationDefinition> Definitions { get; }

    private readonly Dictionary<string, Type?> _definitionProviders;

    private Type? _currentProviderType;

    public NotificationDefinitionContext()
    {
        Definitions = new Dictionary<string, NotificationDefinition>(StringComparer.Ordinal);
        _definitionProviders = new Dictionary<string, Type?>(StringComparer.Ordinal);
    }

    public NotificationDefinition Add(NotificationDefinition definition)
    {
        Check.NotNull(definition, nameof(definition));

        if (Definitions.ContainsKey(definition.Name))
        {
            var providerNames = new[]
                {
                    GetProviderName(_definitionProviders[definition.Name]),
                    GetProviderName(_currentProviderType)
                }
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            throw new InvalidOperationException(
                $"Notification definition name '{definition.Name}' is registered by conflicting providers " +
                $"'{providerNames[0]}' and '{providerNames[1]}'. Definition names use ordinal, case-sensitive comparison.");
        }

        Definitions.Add(definition.Name, definition);
        _definitionProviders.Add(definition.Name, _currentProviderType);
        return definition;
    }

    public NotificationDefinition? GetOrNull(string name)
    {
        return Definitions.TryGetValue(name, out var definition) ? definition : null;
    }

    internal void SetCurrentProvider(Type providerType)
    {
        _currentProviderType = Check.NotNull(providerType, nameof(providerType));
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
