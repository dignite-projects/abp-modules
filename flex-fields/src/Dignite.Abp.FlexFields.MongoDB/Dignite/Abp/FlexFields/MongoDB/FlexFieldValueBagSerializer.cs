using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// How a flex field value bag is written to and read from BSON. The MongoDB counterpart of the EF Core
/// provider's <c>AbpJsonValueConverter&lt;FlexFieldDictionary&gt;</c>: each provider has to say how the bag
/// is stored, because the bag is the kernel's shape rather than the downstream's.
/// <para>
/// Here it carries more weight than a JSON converter does, because this provider queries the bag in place.
/// A relational provider can store the bag however it likes and still answer queries, since it queries a
/// pivot table instead. Every filter this provider builds compares against the bag itself, so the stored
/// BSON <i>is</i> the query surface.
/// </para>
/// <para>
/// Scoped to the kernel's own dictionary types deliberately. Fixing the same problems by replacing the
/// global <c>ObjectSerializer</c> would change how every other <c>Dictionary&lt;string, object&gt;</c> in the
/// host application serializes, which is not this package's to decide.
/// </para>
/// </summary>
/// <typeparam name="TDictionary">The bag type - <see cref="FlexFieldDictionary"/> or
/// <see cref="FieldConfigurationDictionary"/>.</typeparam>
public class FlexFieldValueBagSerializer<TDictionary>
    : DictionaryInterfaceImplementerSerializer<TDictionary, string, object>
    where TDictionary : class, IDictionary<string, object>
{
    public FlexFieldValueBagSerializer()
        : base(DictionaryRepresentation.Document, new StringSerializer(), new FlexFieldBagValueSerializer())
    {
    }
}

/// <summary>
/// Writes a bag value as the BSON type a query can actually address.
/// <para>
/// A bag is a <c>Dictionary&lt;string, object&gt;</c>, so every value reaches the driver boxed, and the
/// driver's <c>ObjectSerializer</c> falls back to a discriminated document -
/// <c>{ "_t": ..., "_v": ... }</c> - for anything without a native BSON counterpart. That round-trips
/// perfectly and queries not at all: a decimal written that way is a sub-document, so no numeric range
/// filter reaches it, and a list written that way is a sub-document too, so no array-element match reaches
/// it either. Both were found by asserting on stored BSON types; a round-trip test passes right through
/// them.
/// </para>
/// <para>
/// Two cases therefore get written explicitly:
/// </para>
/// <list type="bullet">
/// <item><description><b>decimal</b> as BSON Decimal128 - the type
/// <see cref="FlexFieldValueType.Number"/> canonicalizes to, and the one MongoDB compares numerically
/// against every other numeric type.</description></item>
/// <item><description><b>sequences</b> as BSON arrays, so a multi-valued field (Select, Tree) is matched
/// element-wise by the same equality filter a single-valued field uses - the document-store equivalent of
/// the relational provider fanning one value out into several index rows.</description></item>
/// <item><description><b><see cref="JsonElement"/></b> unwrapped into the BSON its own <c>ValueKind</c>
/// names, recursively for an array or object. A bag value most commonly arrives this way -
/// <see cref="FlexibleEntityDto"/> exists specifically to carry the bag across a service boundary, and
/// System.Text.Json request-body binding into a <c>Dictionary&lt;string, object&gt;</c>-shaped property
/// leaves every value as a <see cref="JsonElement"/>. A <see cref="JsonElement"/> has no settable members
/// for a class-map-based serializer to find, so falling through to the driver's own serializer for it does
/// not throw - it silently writes only a discriminator and no content at all.</description></item>
/// </list>
/// <para>
/// Everything else - string, the integral and floating types, DateTime, bool, Guid - already has a native
/// BSON counterpart and goes to the driver's own serializer, configured with
/// <see cref="GuidRepresentation.Standard"/> so a boxed Guid is the same bytes as ABP writes for a typed
/// one. Without that, the driver refuses a boxed Guid outright rather than guessing a binary subtype.
/// </para>
/// </summary>
public class FlexFieldBagValueSerializer : SerializerBase<object>
{
    /// <summary>
    /// Reached only for a CLR object graph built by in-process code - a field type's own configuration
    /// (<see cref="Select.SelectConfiguration.Options"/> is a <c>List&lt;SelectListItem&gt;</c>, a list of a
    /// plain class) is the concrete case this exists for. Every shape that could instead have arrived from
    /// outside the process - a <see cref="JsonElement"/> off a request body, most visibly - is intercepted
    /// above and never reaches this serializer at all, so <see cref="ObjectSerializer.AllAllowedTypes"/> is
    /// widening what first-party code may construct, not what a caller may cause to be reconstructed from
    /// stored BSON. The driver's own default <see cref="ObjectSerializer.DefaultAllowedTypes"/> hardening
    /// against a caller-chosen discriminator remains meaningful for a <c>Dictionary&lt;string, object&gt;</c>
    /// value nested inside one of those graphs (see <see cref="FlexFieldBagValueSerializer_Tests"/>): a
    /// dictionary is written as its own discriminated <c>{_t, _v}</c> wrapper, so a caller-supplied key
    /// named <c>"_t"</c> lands inside the wrapper's <c>_v</c> as inert data, never as the wrapper's own
    /// discriminator.
    /// </summary>
    private readonly IBsonSerializer<object> _fallback = new ObjectSerializer(
        BsonSerializer.LookupDiscriminatorConvention(typeof(object)),
        GuidRepresentation.Standard,
        ObjectSerializer.AllAllowedTypes);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        switch (value)
        {
            case null:
                context.Writer.WriteNull();
                return;

            case decimal decimalValue:
                context.Writer.WriteDecimal128(decimalValue);
                return;

            case DateTime dateTime:
                context.Writer.WriteDateTime(BsonUtils.ToMillisecondsSinceEpoch(ToUtc(dateTime)));
                return;

            case JsonElement element:
                SerializeJsonElement(context.Writer, element);
                return;

            // Before IEnumerable: a string is a sequence of chars, and a dictionary is a sequence of pairs -
            // neither is a multi-valued field value.
            case string:
            case IDictionary:
                _fallback.Serialize(context, args, value);
                return;

            case IEnumerable items:
                context.Writer.WriteStartArray();
                foreach (var item in items)
                {
                    Serialize(context, args, item);
                }
                context.Writer.WriteEndArray();
                return;

            default:
                _fallback.Serialize(context, args, value);
                return;
        }
    }

    /// <summary>
    /// Writes a <see cref="JsonElement"/> as the BSON its own <see cref="JsonValueKind"/> names, recursively
    /// for <see cref="JsonValueKind.Array"/> and <see cref="JsonValueKind.Object"/> - not by handing it to
    /// this type's own <see cref="Serialize"/>, because a nested number/string/bool inside the element is
    /// still a <see cref="JsonElement"/> at that point, not yet a CLR <c>decimal</c>/<c>string</c>/<c>bool</c>
    /// that a recursive <see cref="Serialize"/> call could dispatch on.
    /// </summary>
    private static void SerializeJsonElement(IBsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNull();
                return;

            case JsonValueKind.String:
                writer.WriteString(element.GetString());
                return;

            case JsonValueKind.True:
                writer.WriteBoolean(true);
                return;

            case JsonValueKind.False:
                writer.WriteBoolean(false);
                return;

            case JsonValueKind.Number:
                // A whole number keeps its integral BSON type rather than always widening to Decimal128 -
                // matching FlexFieldValueConverter.Unwrap, which reads the same JsonElement shape the other
                // direction for the same reason.
                if (element.TryGetInt64(out var integer))
                {
                    writer.WriteInt64(integer);
                }
                else
                {
                    writer.WriteDecimal128(element.GetDecimal());
                }
                return;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    SerializeJsonElement(writer, item);
                }
                writer.WriteEndArray();
                return;

            case JsonValueKind.Object:
                // Not routed through the dictionary-wrapping _fallback path: unlike a real
                // Dictionary<string, object>, a JSON object's own shape is exactly a set of named BSON
                // members, with no ambiguity a discriminator would need to resolve.
                writer.WriteStartDocument();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WriteName(property.Name);
                    SerializeJsonElement(writer, property.Value);
                }
                writer.WriteEndDocument();
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(element), element.ValueKind, "Unknown JsonValueKind.");
        }
    }

    public override object? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;

        switch (reader.GetCurrentBsonType())
        {
            case BsonType.Null:
                reader.ReadNull();
                return null;

            case BsonType.Decimal128:
                return Decimal128.ToDecimal(reader.ReadDecimal128());

            case BsonType.DateTime:
                return BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadDateTime());

            case BsonType.Array:
                return DeserializeArray(context, args);

            default:
                return _fallback.Deserialize(context, args);
        }
    }

    /// <summary>
    /// A bag <see cref="System.DateTime"/> with no <see cref="DateTimeKind"/> is read as UTC rather than as
    /// server-local.
    /// <para>
    /// The driver's own default is the opposite - it treats <see cref="DateTimeKind.Unspecified"/> as local
    /// and converts, so a value parsed from the offset-less text a
    /// <see cref="FlexFieldQueryCondition.Value"/> carries comes back shifted by the server's timezone.
    /// Storing an instant that depends on which server wrote it is not a defensible reading of transport
    /// data, and it would put the stored value and every condition built from that same text on opposite
    /// sides of the shift.
    /// </para>
    /// <para>
    /// A value that does carry a kind is honoured: a local time really is converted.
    /// </para>
    /// </summary>
    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    /// <summary>
    /// Reads back as <c>List&lt;object&gt;</c> rather than a typed list, because the element type is not
    /// recorded - nothing wrote a discriminator. That is the shape
    /// <c>FieldTypeBase.ReadStringList</c> exists to accept.
    /// </summary>
    private List<object> DeserializeArray(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var items = new List<object>();

        reader.ReadStartArray();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            items.Add(Deserialize(context, args)!);
        }
        reader.ReadEndArray();

        return items;
    }
}
