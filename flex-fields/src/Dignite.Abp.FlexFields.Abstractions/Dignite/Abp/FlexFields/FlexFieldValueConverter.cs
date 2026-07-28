using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// The single definition of what a <see cref="FlexFieldValueType"/> means as a CLR value. Both directions
/// into a comparison go through here: a raw value out of a host's bag
/// (<see cref="IFieldType.GetSearchableValues"/>) via <see cref="Coerce"/>, and a
/// <see cref="FlexFieldQueryCondition.Value"/> off the wire via <see cref="Parse"/>.
/// <para>
/// <b>Invariant culture throughout, and that is the whole point.</b> Both a bag value and a condition value
/// are transport data - neither is a localized number a user typed - so neither may be read against whatever
/// culture the request happens to run under. <see cref="Parse"/> is literally <see cref="Coerce"/>, because
/// <c>Convert.To*(string, CultureInfo.InvariantCulture)</c> <i>is</i> the invariant parse: the two sides
/// cannot drift apart because there is only one implementation.
/// </para>
/// <para>
/// Provider-neutral on purpose. A relational provider pairs the result with a typed column
/// (<c>FlexFieldIndexValue</c>); a document store writes it straight back into the bag so its native path
/// index has a well-typed value to index. The conversion rules are the same either way - only what is done
/// with the result differs.
/// </para>
/// </summary>
public static class FlexFieldValueConverter
{
    /// <summary>
    /// Reads a raw value - out of a value bag, so it may be a <see cref="JsonElement"/> left by a JSON round
    /// trip, a string, or an already-typed CLR value - as the type named by <paramref name="valueType"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="valueType"/> is not a known member.</exception>
    public static object Coerce(FlexFieldValueType valueType, object rawValue)
    {
        if (rawValue == null)
        {
            throw new ArgumentNullException(nameof(rawValue));
        }

        var value = Unwrap(rawValue);

        return valueType switch
        {
            FlexFieldValueType.String => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            FlexFieldValueType.Number => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            FlexFieldValueType.DateTime => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
            FlexFieldValueType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
            // Convert has no Guid support - a Guid is either already one or its textual form.
            FlexFieldValueType.Guid => value as Guid? ?? Guid.Parse(value.ToString()!),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType), valueType, null)
        };
    }

    /// <summary>
    /// Reads a <see cref="FlexFieldQueryCondition.Value"/> as the type named by <paramref name="valueType"/>,
    /// so it compares against whatever <see cref="Coerce"/> produced for the stored value.
    /// </summary>
    public static object Parse(FlexFieldValueType valueType, string value)
    {
        return Coerce(valueType, value);
    }

    /// <summary>
    /// Reads the comma-separated <see cref="FlexFieldQueryCondition.Value"/> of a
    /// <see cref="FlexFieldQueryOperator.In"/> condition.
    /// </summary>
    public static IReadOnlyList<object> ParseList(FlexFieldValueType valueType, string value)
    {
        return SplitValues(value).Select(item => Parse(valueType, item)).ToList();
    }

    /// <summary>
    /// Splits an <see cref="FlexFieldQueryOperator.In"/> condition's value into its members. Empty entries
    /// are dropped and each member is trimmed, so <c>"red, blue"</c> and <c>"red,blue"</c> are the same set.
    /// </summary>
    public static List<string> SplitValues(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Turns a <see cref="JsonElement"/> into the CLR primitive it stands for. A value bag is a
    /// <c>Dictionary&lt;string, object&gt;</c>, so any JSON deserializer that does not infer types leaves
    /// <see cref="JsonElement"/>s behind - most visibly on the inbound DTO path. Anything else is returned
    /// as-is.
    /// </summary>
    private static object Unwrap(object rawValue)
    {
        if (rawValue is not JsonElement element)
        {
            return rawValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            // Null/Undefined/Object/Array have no scalar reading; hand back the raw JSON text and let the
            // target-type conversion below produce the error, which names the value type that failed.
            _ => element.ToString()
        };
    }
}
