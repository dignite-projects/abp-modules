using System;
using System.Text.Json;

namespace Dignite.Abp.FlexFields;

public static class FlexFieldDictionaryExtensions
{
    public static bool HasField(this IHasFlexFields source, string name)
    {
        return source.FlexFields.ContainsKey(name);
    }

    public static object? GetField(this IHasFlexFields source, string name, object? defaultValue = null)
    {
        return source.FlexFields.TryGetValue(name, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Typed read of a field's value. After a JSON round trip (EF value-bag column, Mongo driver, ...)
    /// non-primitive values commonly come back as <see cref="JsonElement"/> rather than their original
    /// CLR type - this unwraps that case instead of throwing an invalid-cast exception.
    /// </summary>
    public static TField GetField<TField>(this IHasFlexFields source, string name, TField defaultValue = default!)
    {
        var value = source.GetField(name);
        switch (value)
        {
            case null:
                return defaultValue;
            case TField typed:
                return typed;
            case JsonElement element:
                return element.Deserialize<TField>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? defaultValue;
            default:
                return (TField)Convert.ChangeType(value, typeof(TField));
        }
    }

    public static TSource SetField<TSource>(this TSource source, string name, object? value)
        where TSource : IHasFlexFields
    {
        if (value == null)
        {
            source.FlexFields.Remove(name);
        }
        else
        {
            source.FlexFields[name] = value;
        }

        return source;
    }

    public static TSource RemoveField<TSource>(this TSource source, string name)
        where TSource : IHasFlexFields
    {
        source.FlexFields.Remove(name);
        return source;
    }
}
