using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

public class NotificationDataTypeRegistry :
    INotificationDataTypeRegistry,
    ISingletonDependency
{
    private readonly Dictionary<string, Type> _byDiscriminator;
    private readonly Dictionary<Type, string> _byType;

    public NotificationDataTypeRegistry(IOptions<NotificationDataOptions> options)
    {
        _byDiscriminator = new Dictionary<string, Type>(StringComparer.Ordinal);
        _byType = new Dictionary<Type, string>();

        foreach (var pair in options.Value.DataTypes)
        {
            if (_byType.TryGetValue(pair.Value, out var registeredDiscriminator))
            {
                throw new InvalidOperationException(
                    $"Notification data CLR type '{GetTypeName(pair.Value)}' is registered with " +
                    $"conflicting discriminators '{registeredDiscriminator}' and '{pair.Key}'.");
            }

            _byDiscriminator.Add(pair.Key, pair.Value);
            _byType.Add(pair.Value, pair.Key);
        }
    }

    public string? GetDiscriminatorOrNull(Type dataType)
    {
        return _byType.TryGetValue(dataType, out var name) ? name : null;
    }

    public Type? GetTypeOrNull(string discriminator)
    {
        return _byDiscriminator.TryGetValue(discriminator, out var type) ? type : null;
    }

    private static string GetTypeName(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "<unknown assembly>";
        return $"{type.FullName ?? type.Name}, {assemblyName}";
    }
}
