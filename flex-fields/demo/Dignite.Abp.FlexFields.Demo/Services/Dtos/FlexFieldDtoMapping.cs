using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.FlexFields.Demo.Services.Dtos;

/// <summary>
/// Hand-written conversions Mapperly is deliberately not asked to do - see the remarks in
/// <c>ObjectMapping/DemoMappers.cs</c>. Generic collection types are invariant in C#
/// (<c>Dictionary&lt;string, object?&gt;</c> and <c>Dictionary&lt;string, object&gt;</c> are unrelated as
/// far as the type system is concerned, even though every value in one is assignable to the other), so a
/// dictionary crossing the kernel/DTO boundary always needs an explicit copy like this - never a direct
/// cast or constructor call.
/// </summary>
public static class FlexFieldDtoMapping
{
    public static FieldConfigurationDictionary ToConfiguration(this Dictionary<string, object?> source)
    {
        var configuration = new FieldConfigurationDictionary();

        foreach (var (key, value) in source)
        {
            configuration[key] = value!;
        }

        return configuration;
    }

    public static Dictionary<string, object?> ToDictionary(this FieldConfigurationDictionary source)
    {
        return source.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
    }

    public static FlexFieldDataDto ToDto(this IFlexFieldData field)
    {
        return new FlexFieldDataDto
        {
            Id = field.Id,
            Name = field.Name,
            DisplayName = field.DisplayName,
            Description = field.Description,
            FieldTypeName = field.FieldTypeName,
            Configuration = field.Configuration.ToDictionary(),
        };
    }

    public static FlexFieldValueDto ToDto(this FlexFieldValue value)
    {
        return new FlexFieldValueDto
        {
            Field = value.Field.ToDto(),
            Required = value.Required,
            Searchable = value.Searchable,
            Value = value.Value,
        };
    }
}
