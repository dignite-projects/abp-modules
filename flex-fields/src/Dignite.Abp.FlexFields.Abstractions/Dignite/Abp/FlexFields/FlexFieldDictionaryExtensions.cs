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

    /// <summary>
    /// Moves the value stored under <paramref name="oldName"/> to <paramref name="newName"/>, returning
    /// whether anything actually changed. A field definition's name <i>is</i> the bag key, so renaming a
    /// definition without doing this to every host entity turns its values into orphan keys that no read
    /// path can reach.
    /// <para>
    /// A missing <paramref name="oldName"/> is not an error - it returns <c>false</c> and touches nothing,
    /// which is what makes a paged migration safe to re-run after a partial failure.
    /// </para>
    /// <para>
    /// <see cref="FlexFieldDictionary"/> inherits <c>Dictionary&lt;string, object&gt;</c>'s default ordinal
    /// comparer, so a case-only rename ("authorName" to "AuthorName") is a real move, not a no-op.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="newName"/> already holds a value while <paramref name="oldName"/> also exists.
    /// Silently picking a winner would lose data; the caller was meant to rule the collision out first with
    /// <c>IFlexFieldRepository{TField}.NameExistsAsync(newName, excludedId)</c>.
    /// </exception>
    public static bool RenameField(this IHasFlexFields source, string oldName, string newName)
    {
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!source.FlexFields.TryGetValue(oldName, out var value))
        {
            return false;
        }

        if (source.FlexFields.ContainsKey(newName))
        {
            throw new InvalidOperationException(
                $"Cannot rename flex field '{oldName}' to '{newName}': the value bag already holds a value " +
                $"under '{newName}'.");
        }

        source.FlexFields.Remove(oldName);
        source.FlexFields[newName] = value;
        return true;
    }
}
