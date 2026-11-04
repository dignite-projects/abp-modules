using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Volo.Abp;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// One configuration key a field type understands: what it is called, what kind of value it takes, what
/// that value is when the key is absent, and what it is for. A field type lists its keys through
/// <see cref="IFieldType.ConfigurationKeys"/>.
/// <para>
/// <b>Why this exists.</b> A field's configuration is a <c>Dictionary&lt;string, object&gt;</c>, and a
/// dictionary accepts anything. A client that spells a key wrongly (<c>Text.Multiline</c> for
/// <c>Text.Mode</c>) is not told: the value is stored, nothing reads it, and the field behaves as if it had
/// never been set. Declaring the keys is what lets the kernel say so (<see cref="IFieldConfigurationValidator"/>)
/// and lets a client ask before it writes (the field type listing a host serves).
/// </para>
/// <para>
/// <b>The key names are stored data</b>, exactly as <see cref="IFieldType.Name"/> is: they are what every
/// saved field definition holds. A declaration restates the names the field type's
/// <c>*ConfigurationNames</c> class already defines - it never invents or renames one.
/// </para>
/// </summary>
public sealed class FieldConfigurationKey
{
    private static readonly JsonSerializerOptions ShapeNaming = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Declares a key.
    /// </summary>
    /// <param name="name">The key as stored, e.g. <c>Text.CharLimit</c>. Case-sensitive.</param>
    /// <param name="valueKind">The JSON kind of the value.</param>
    /// <param name="valueType">
    /// Only for <see cref="FieldConfigurationValueKind.Enum"/> (required: the enum, whose members are listed
    /// as <see cref="EnumMembers"/>) and for <see cref="FieldConfigurationValueKind.Array"/> /
    /// <see cref="FieldConfigurationValueKind.Object"/> (optional: the CLR type the value is read as, which
    /// the validator then deserializes it into, and which <see cref="Shape"/> is described from). Not
    /// allowed for the scalar kinds.
    /// </param>
    public FieldConfigurationKey(string name, FieldConfigurationValueKind valueKind, Type? valueType = null)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ValueKind = valueKind;

        switch (valueKind)
        {
            case FieldConfigurationValueKind.Enum:
                if (valueType is not { IsEnum: true })
                {
                    throw new ArgumentException(
                        $"The configuration key '{name}' is an Enum, so its value type must be an enum type.", nameof(valueType));
                }

                EnumMembers = Enum.GetValues(valueType)
                    .Cast<object>()
                    .Select(member => new FieldConfigurationEnumMember(Convert.ToInt32(member), Enum.GetName(valueType, member)!))
                    .ToList();
                break;

            case FieldConfigurationValueKind.Array:
            case FieldConfigurationValueKind.Object:
                break;

            default:
                if (valueType != null)
                {
                    throw new ArgumentException(
                        $"The configuration key '{name}' is a {valueKind}, which takes no value type.", nameof(valueType));
                }

                break;
        }

        ValueType = valueType;
    }

    /// <summary>The key as stored, e.g. <c>Text.CharLimit</c>. Case-sensitive.</summary>
    public string Name { get; }

    public FieldConfigurationValueKind ValueKind { get; }

    /// <summary>See the constructor's <c>valueType</c>.</summary>
    public Type? ValueType { get; }

    /// <summary>
    /// What the key is for, in a sentence, for a developer or an AI client choosing what to write. Plain
    /// English, deliberately not localized: it documents a wire key, the way the key itself is not translated.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// What the field behaves as when the key is absent. A JSON-compatible value, so an enum's default is its
    /// integer. Null when there is no fixed default - the key is simply unset, or the default depends on
    /// another key (say so in <see cref="Description"/>).
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Whether a field of this type cannot work without the key. False for every built-in key: each has a
    /// default or is simply unset. For a <see cref="FieldConfigurationValueKind.String"/> key an empty string
    /// counts as missing - a container name of <c>""</c> names nothing.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Whether an empty string means "not set", for a number, date or GUID. The Angular editors send one for
    /// an empty box (<c>"DateTime.Min": ""</c>), and the kernel reads it as unset. Without this, an empty
    /// string for such a key is a wrong value: it does not read back. <c>null</c> always means unset, whatever
    /// this says. Meaningless for a string key, where an empty string is simply a string.
    /// </summary>
    public bool EmptyStringIsUnset { get; init; }

    /// <summary>The members of an <see cref="FieldConfigurationValueKind.Enum"/> key; null for every other kind.</summary>
    public IReadOnlyList<FieldConfigurationEnumMember>? EnumMembers { get; }

    /// <summary>
    /// The value the key takes, written as JSON would show it: <c>integer</c>, <c>boolean</c>,
    /// <c>integer: 0 (SingleLine) | 1 (MultipleLine)</c>, or for a shaped array
    /// <c>[{ "text": string, "value": string, "selected": boolean }]</c>. Derived from
    /// <see cref="ValueKind"/> and <see cref="ValueType"/>, so it cannot drift from what the validator accepts.
    /// </summary>
    public string Shape
    {
        get
        {
            switch (ValueKind)
            {
                case FieldConfigurationValueKind.String:
                    return "string";
                case FieldConfigurationValueKind.Integer:
                    return "integer";
                case FieldConfigurationValueKind.Number:
                    return "number";
                case FieldConfigurationValueKind.Boolean:
                    return "boolean";
                case FieldConfigurationValueKind.DateTime:
                    return "ISO 8601 date-time string";
                case FieldConfigurationValueKind.Guid:
                    return "GUID string";
                case FieldConfigurationValueKind.Enum:
                    return "integer: " + DescribeEnumMembers();
                case FieldConfigurationValueKind.Array:
                    return ValueType == null ? "array" : DescribeType(ValueType, new HashSet<Type>());
                default:
                    return ValueType == null ? "object" : DescribeType(ValueType, new HashSet<Type>());
            }
        }
    }

    /// <summary>The members of an enum key as <c>0 (SingleLine) | 1 (MultipleLine)</c>.</summary>
    public string DescribeEnumMembers()
    {
        return string.Join(" | ", (EnumMembers ?? Array.Empty<FieldConfigurationEnumMember>())
            .Select(member => $"{member.Value} ({member.Name})"));
    }

    private static string DescribeType(Type type, HashSet<Type> visiting)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type.IsEnum)
        {
            return "integer";
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
        {
            return "integer";
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            return "number";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return "ISO 8601 date-time string";
        }

        if (type == typeof(Guid))
        {
            return "GUID string";
        }

        if (type == typeof(object))
        {
            return "any";
        }

        if (ImplementsGeneric(type, typeof(IDictionary<,>)))
        {
            return "object";
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable != null)
        {
            return "[" + DescribeType(enumerable.GetGenericArguments()[0], visiting) + "]";
        }

        // A type that contains itself (a tree node's children) is named the second time round, not expanded.
        if (!visiting.Add(type))
        {
            return type.Name;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Select(property => $"\"{ShapeNaming.PropertyNamingPolicy!.ConvertName(property.Name)}\": {DescribeType(property.PropertyType, visiting)}")
            .ToList();

        visiting.Remove(type);

        return "{ " + string.Join(", ", properties) + " }";
    }

    private static bool ImplementsGeneric(Type type, Type genericDefinition)
    {
        return (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
               || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition);
    }
}
