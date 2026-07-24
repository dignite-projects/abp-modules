using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Polymorphic (de)serializer for <see cref="NotificationData"/>. It owns the stable <c>type</c> discriminator
/// envelope and never resolves CLR names from JSON. Reads are always tolerant: an unknown discriminator or a
/// malformed known payload becomes <see cref="UnsupportedNotificationData"/> instead of throwing, so one bad
/// payload cannot break a whole page/event. Writing an unregistered CLR type still throws.
/// </summary>
public sealed class NotificationDataJsonConverter : JsonConverter<NotificationData>
{
    public const string DiscriminatorPropertyName = "type";

    private readonly INotificationDataTypeRegistry _registry;
    private JsonSerializerOptions? _innerOptions;

    public NotificationDataJsonConverter(INotificationDataTypeRegistry registry)
    {
        _registry = registry;
    }

    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(NotificationData).IsAssignableFrom(typeToConvert);
    }

    public override NotificationData? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return HandleFailure(UnsupportedNotificationDataReason.MalformedPayload, rawJson: string.Empty);
        }

        using (document)
        {
            var root = document.RootElement;
            var rawJson = root.GetRawText();
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HandleFailure(UnsupportedNotificationDataReason.MalformedPayload, rawJson);
            }

            if (!root.TryGetProperty(DiscriminatorPropertyName, out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(typeElement.GetString()))
            {
                return HandleFailure(UnsupportedNotificationDataReason.MalformedPayload, rawJson);
            }

            var discriminator = typeElement.GetString()!;
            var clrType = _registry.GetTypeOrNull(discriminator);
            if (clrType == null)
            {
                return HandleFailure(UnsupportedNotificationDataReason.UnknownDiscriminator, rawJson, discriminator);
            }

            JsonObject payload;
            try
            {
                payload = CreatePayloadObject(root);
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                return HandleFailure(UnsupportedNotificationDataReason.MalformedPayload, rawJson, discriminator);
            }

            try
            {
                var data = (NotificationData?)JsonSerializer.Deserialize(
                    payload.ToJsonString(),
                    clrType,
                    GetInnerOptions(options));
                if (data == null)
                {
                    throw new JsonException($"Notification data '{discriminator}' deserialized to null.");
                }

                return data;
            }
            catch (Exception exception) when (IsRecoverableReadException(exception))
            {
                return HandleFailure(UnsupportedNotificationDataReason.MalformedPayload, rawJson, discriminator);
            }
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        NotificationData value,
        JsonSerializerOptions options)
    {
        var clrType = value.GetType();
        var discriminator = _registry.GetDiscriminatorOrNull(clrType)
            ?? throw new JsonException(
                $"Notification data type '{clrType.FullName}' is not registered. " +
                "Annotate it with [NotificationDataType(\"...\")] and register it via NotificationDataOptions.");

        using var document = JsonSerializer.SerializeToDocument(value, clrType, GetInnerOptions(options));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"Notification data type '{clrType.FullName}' must serialize as a JSON object.");
        }

        writer.WriteStartObject();
        writer.WriteString(DiscriminatorPropertyName, discriminator);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, DiscriminatorPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static UnsupportedNotificationData HandleFailure(
        UnsupportedNotificationDataReason reason,
        string rawJson,
        string? discriminator = null)
    {
        return new UnsupportedNotificationData
        {
            OriginalDiscriminator = discriminator,
            Reason = reason,
            RawJson = rawJson
        };
    }

    private static JsonObject CreatePayloadObject(JsonElement root)
    {
        var payload = new JsonObject();
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, DiscriminatorPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            payload.Add(property.Name, JsonNode.Parse(property.Value.GetRawText()));
        }

        return payload;
    }

    /// <summary>Shared with <see cref="NotificationDataSerializer"/>'s own top-level read guard.</summary>
    internal static bool IsRecoverableReadException(Exception exception)
    {
        return exception is not OperationCanceledException &&
               exception is not OutOfMemoryException &&
               exception is not StackOverflowException &&
               exception is not AccessViolationException &&
               exception is not AppDomainUnloadedException &&
               exception is not BadImageFormatException &&
               exception is not CannotUnloadAppDomainException &&
               exception is not InvalidProgramException;
    }

    /// <summary>
    /// A sibling options set without this converter, used to (de)serialize the concrete type without re-entering here.
    /// </summary>
    private JsonSerializerOptions GetInnerOptions(JsonSerializerOptions options)
    {
        if (_innerOptions is null)
        {
            var inner = new JsonSerializerOptions(options);
            for (var index = inner.Converters.Count - 1; index >= 0; index--)
            {
                if (inner.Converters[index] is NotificationDataJsonConverter)
                {
                    inner.Converters.RemoveAt(index);
                }
            }

            _innerOptions = inner;
        }

        return _innerOptions;
    }
}
