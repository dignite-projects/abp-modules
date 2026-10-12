using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Channel name → <see cref="INotificationNotifier"/> type registrations. Repeating the exact channel/type pair is
/// idempotent; a channel claimed by two types, or a type claimed for two channels, is rejected as it is added —
/// which, because <see cref="NotificationNotifierOptions"/> is materialized at startup, fails the application start.
/// </summary>
public sealed class NotificationNotifierDictionary : IReadOnlyDictionary<string, Type>
{
    private readonly Dictionary<string, Type> _items = new(StringComparer.OrdinalIgnoreCase);

    public Type this[string key] => _items[key];

    public IEnumerable<string> Keys => _items.Keys;

    public IEnumerable<Type> Values => _items.Values;

    public int Count => _items.Count;

    public NotificationNotifierDictionary Add<TNotifier>(string channel)
        where TNotifier : INotificationNotifier
    {
        return Add(channel, typeof(TNotifier));
    }

    public NotificationNotifierDictionary Add(string channel, Type notifierType)
    {
        Check.NotNullOrWhiteSpace(channel, nameof(channel));
        Check.AssignableTo<INotificationNotifier>(notifierType, nameof(notifierType));

        if (_items.TryGetValue(channel, out var registeredType))
        {
            if (registeredType == notifierType)
            {
                return this;
            }

            var typeNames = new[] { GetTypeName(registeredType), GetTypeName(notifierType) }
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            throw new InvalidOperationException(
                $"Multiple notification notifiers are registered for channel '{channel}': '{typeNames[0]}' and " +
                $"'{typeNames[1]}'. To customize a channel's notifier, replace its registered type in dependency " +
                "injection instead of registering a second type.");
        }

        var registeredChannel = _items
            .Where(pair => pair.Value == notifierType)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (registeredChannel != null)
        {
            throw new InvalidOperationException(
                $"Notification notifier '{GetTypeName(notifierType)}' is registered for channels " +
                $"'{registeredChannel}' and '{channel}'. A notifier serves the one channel its Name returns.");
        }

        _items.Add(channel, notifierType);
        return this;
    }

    public bool ContainsKey(string key)
    {
        return _items.ContainsKey(key);
    }

    public bool TryGetValue(string key, out Type value)
    {
        return _items.TryGetValue(key, out value!);
    }

    public IEnumerator<KeyValuePair<string, Type>> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static string GetTypeName(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "<unknown assembly>";
        return $"{type.FullName ?? type.Name}, {assemblyName}";
    }
}
