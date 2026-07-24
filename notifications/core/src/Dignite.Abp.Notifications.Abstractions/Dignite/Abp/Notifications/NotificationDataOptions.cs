using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Registers the <see cref="NotificationData"/> types known to the application, keyed by their stable
/// discriminator. Business modules add their own types here so those types can be resolved on any client.
/// Discriminators use ordinal, case-sensitive comparison. Repeating the exact discriminator/type pair is
/// idempotent; every other discriminator or CLR type conflict is rejected during application startup.
/// </summary>
public class NotificationDataOptions
{
    /// <summary>
    /// Gets the registered discriminator-to-type mappings. Keys use ordinal, case-sensitive comparison.
    /// Use <see cref="Add(string, Type)"/> to preserve conflict validation.
    /// </summary>
    public IDictionary<string, Type> DataTypes { get; }

    public NotificationDataOptions()
    {
        DataTypes = new NotificationDataTypeDictionary();
    }

    public NotificationDataOptions Add<TData>() where TData : NotificationData
    {
        return Add(typeof(TData));
    }

    public NotificationDataOptions Add(Type dataType)
    {
        Check.NotNull(dataType, nameof(dataType));

        var name = NotificationDataTypeAttribute.GetNameOrNull(dataType)
            ?? throw new ArgumentException(
                $"'{dataType.FullName}' must be annotated with [NotificationDataType(\"...\")] to be registered.",
                nameof(dataType));

        return Add(name, dataType);
    }

    public NotificationDataOptions Add(string discriminator, Type dataType)
    {
        DataTypes.Add(discriminator, dataType);
        return this;
    }

    private sealed class NotificationDataTypeDictionary : IDictionary<string, Type>
    {
        private readonly Dictionary<string, Type> _items = new(StringComparer.Ordinal);

        public Type this[string key]
        {
            get => _items[key];
            set => Add(key, value);
        }

        public ICollection<string> Keys => _items.Keys;

        public ICollection<Type> Values => _items.Values;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(string discriminator, Type dataType)
        {
            Check.NotNullOrWhiteSpace(discriminator, nameof(discriminator));
            Check.AssignableTo<NotificationData>(dataType, nameof(dataType));

            if (_items.TryGetValue(discriminator, out var registeredType))
            {
                if (registeredType == dataType)
                {
                    return;
                }

                var typeNames = new[] { GetTypeName(registeredType), GetTypeName(dataType) }
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                throw new InvalidOperationException(
                    $"Notification data discriminator '{discriminator}' is registered for conflicting CLR types " +
                    $"'{typeNames[0]}' and '{typeNames[1]}'. Discriminators use ordinal, case-sensitive comparison.");
            }

            var registeredDiscriminator = _items
                .Where(pair => pair.Value == dataType)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (registeredDiscriminator != null)
            {
                var discriminators = new[] { registeredDiscriminator, discriminator }
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                throw new InvalidOperationException(
                    $"Notification data CLR type '{GetTypeName(dataType)}' is registered with conflicting discriminators " +
                    $"'{discriminators[0]}' and '{discriminators[1]}'. Discriminators use ordinal, case-sensitive comparison.");
            }

            _items.Add(discriminator, dataType);
        }

        public void Add(KeyValuePair<string, Type> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(KeyValuePair<string, Type> item)
        {
            return ((ICollection<KeyValuePair<string, Type>>)_items).Contains(item);
        }

        public bool ContainsKey(string key)
        {
            return _items.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, Type>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, Type>>)_items).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, Type>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public bool Remove(string key)
        {
            return _items.Remove(key);
        }

        public bool Remove(KeyValuePair<string, Type> item)
        {
            return ((ICollection<KeyValuePair<string, Type>>)_items).Remove(item);
        }

        public bool TryGetValue(string key, out Type value)
        {
            return _items.TryGetValue(key, out value!);
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
}
