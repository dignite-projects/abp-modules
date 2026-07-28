using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Reflection;

namespace Dignite.Abp.FlexFields;

public static class FieldConfigurationDictionaryExtensions
{
    public static bool HasConfiguration(this FieldConfigurationDictionary source, string name)
    {
        return source.ContainsKey(name);
    }

    public static object? GetConfiguration(this FieldConfigurationDictionary source, string name, object? defaultValue = null)
    {
        return source.GetOrDefault(name)
               ?? defaultValue;
    }

    public static TConfiguration GetConfiguration<TConfiguration>(this FieldConfigurationDictionary source, string name, TConfiguration defaultValue = default)
    {
        var value = source.GetConfiguration(name);
        if (value == null)
        {
            return defaultValue;
        }

        try
        {
            var conversionType = typeof(TConfiguration);
            if (TypeHelper.IsNullable(conversionType))
            {
                conversionType = conversionType.GetFirstGenericArgumentIfNullable();
            }


            if (conversionType.IsEnum)
            {
                if (Enum.IsDefined(conversionType, (int)(long)value))
                {
                    return (TConfiguration)Enum.ToObject(conversionType, (int)(long)value);
                }
                else
                {
                    return defaultValue;
                }
            }

            return (TConfiguration)TypeDescriptor.GetConverter(conversionType).ConvertFromInvariantString(value.ToString()!)!;
        }
        catch (Exception)
        {
            // TypeDescriptor has no string conversion for a collection/object-shaped TConfiguration (e.g.
            // SelectConfiguration.Options is a List<SelectListItem>). If value is already assignable - the
            // in-memory case, where nothing has round-tripped through storage yet - use it directly rather
            // than paying for a JSON round trip. Otherwise value is whatever shape a round trip left behind:
            // a JsonElement after an EF/JSON round trip, or a List<object>/Dictionary<string, object> after a
            // document-driver round trip - neither of which casts to TConfiguration (List<object> is not
            // List<SelectListItem>, even though every element is one). Re-serializing with value's own
            // runtime shape and deserializing into TConfiguration reconstructs it correctly either way.
            if (value is TConfiguration typed)
            {
                return typed;
            }

            return JsonSerializer.Deserialize<TConfiguration>(
                JsonSerializer.Serialize(value), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

    }

    public static void SetConfiguration<TConfiguration>(
        this FieldConfigurationDictionary source,
        string name,
        TConfiguration value)
    {
        source[name] = value;
    }

    public static void RemoveConfiguration(this FieldConfigurationDictionary source, string name)
    {
        source.Remove(name);
    }
}
