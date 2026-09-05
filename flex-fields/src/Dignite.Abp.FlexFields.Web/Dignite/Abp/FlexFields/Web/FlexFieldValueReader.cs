using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Table;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>
/// Lenient readers for a <see cref="FlexFieldValue"/>'s raw <see cref="FlexFieldValue.Value"/>: it may
/// be a native CLR value (still in memory) or a <see cref="JsonElement"/> (after a JSON round trip),
/// and a shipped default view can't assume which one it got - the same lenience
/// <c>FieldTypeBase.ReadStringList</c> already applies to multi-valued fields in the kernel (that
/// method is <c>protected</c>, so not reusable from here - <see cref="ReadStringList"/> mirrors it).
/// Always reads with <see cref="CultureInfo.InvariantCulture"/>, matching how the value was stored;
/// formatting the result for display is the view's job, and should use the current request culture.
/// </summary>
internal static class FlexFieldValueReader
{
    public static string? ReadString(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    public static decimal? ReadDecimal(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonElement { ValueKind: JsonValueKind.Number } element:
                return element.GetDecimal();
            case JsonElement { ValueKind: JsonValueKind.String } element:
                return decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedFromJson)
                    ? parsedFromJson
                    : null;
            case JsonElement:
                return null;
            case IConvertible convertible:
                return Convert.ToDecimal(convertible, CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    public static bool ReadBool(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            JsonElement { ValueKind: JsonValueKind.String } element => bool.TryParse(element.GetString(), out var parsed) && parsed,
            _ => bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed
        };
    }

    public static DateTime? ReadDateTime(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case DateTime dt:
                return dt;
            case JsonElement { ValueKind: JsonValueKind.String } element when element.TryGetDateTime(out var parsedFromJson):
                return parsedFromJson;
            case JsonElement:
                return null;
            default:
                return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed
                    : null;
        }
    }

    /// <summary>
    /// Reads a multi-valued field's raw value as a list of strings: a single scalar reads as a
    /// one-element list, null/absent as empty.
    /// </summary>
    public static List<string> ReadStringList(object? value)
    {
        switch (value)
        {
            case null:
                return new List<string>();
            case List<string> list:
                return list;
            // Before the general IEnumerable case: a string is a sequence of chars, and "abc" is one
            // value, not three.
            case string single:
                return new List<string> { single };
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                return element.EnumerateArray().Select(ReadJsonElement).ToList();
            case JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                return new List<string>();
            case JsonElement element:
                return new List<string> { ReadJsonElement(element) };
            case IEnumerable items:
                return items
                    .Cast<object?>()
                    .Where(item => item != null)
                    .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)!)
                    .ToList();
            default:
                return new List<string> { Convert.ToString(value, CultureInfo.InvariantCulture)! };
        }
    }

    /// <summary>
    /// Reads a <c>Matrix</c> field's raw value as its list of block instances. The composite counterpart
    /// of the scalar readers above, and lenient for exactly the same reason:
    /// <c>MatrixFieldType.ReadBlocks</c> is <c>private</c>, so a view cannot reuse it and gets this
    /// instead.
    /// </summary>
    public static IReadOnlyList<MatrixBlockValue> ReadMatrixBlocks(object? value)
    {
        return value switch
        {
            null => new List<MatrixBlockValue>(),
            List<MatrixBlockValue> list => list,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => new List<MatrixBlockValue>(),
            JsonElement { ValueKind: JsonValueKind.Array } element =>
                element.Deserialize<List<MatrixBlockValue>>(WebOptions) ?? new List<MatrixBlockValue>(),
            _ => new List<MatrixBlockValue>()
        };
    }

    /// <summary>
    /// Reads a <c>Table</c> field's raw value as its list of rows - see
    /// <see cref="ReadMatrixBlocks"/>, whose reasoning this mirrors exactly.
    /// </summary>
    public static IReadOnlyList<TableRow> ReadTableRows(object? value)
    {
        return value switch
        {
            null => new List<TableRow>(),
            List<TableRow> list => list,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => new List<TableRow>(),
            JsonElement { ValueKind: JsonValueKind.Array } element =>
                element.Deserialize<List<TableRow>>(WebOptions) ?? new List<TableRow>(),
            _ => new List<TableRow>()
        };
    }

    private static string ReadJsonElement(JsonElement item)
    {
        return item.ValueKind == JsonValueKind.String ? item.GetString()! : item.ToString();
    }

    /// <summary>
    /// The same <c>JsonSerializerDefaults.Web</c> options the composite field types themselves read and
    /// normalize with - the camelCase wire shape <c>INormalizesValue</c> guarantees on write is the one
    /// these readers have to assume on read.
    /// </summary>
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
}
