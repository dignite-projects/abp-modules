using System;
using System.Globalization;
using System.Text.Json;

namespace Dignite.Abp.FlexFields.CKEditor.Web;

/// <summary>
/// Reads a CKEditor field's raw stored value leniently: a fresh in-memory value is a plain string, one
/// that has round-tripped through JSON storage is a <see cref="JsonElement"/>. Simpler than
/// FileExplorer.Web's own FileDescriptorValueReader (and Dignite.Abp.FlexFields.Web's own internal
/// FlexFieldValueReader, which is internal to that assembly and so not reusable from here) because a
/// CKEditor value is always a bare string - HTML or Markdown source - never an array of objects.
/// </summary>
internal static class CKEditorValueReader
{
    public static string? ReadString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
