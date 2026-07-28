using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Base class for field types.
/// </summary>
public abstract class FieldTypeBase : IFieldType, ITransientDependency
{
    public IAbpLazyServiceProvider LazyServiceProvider { get; set; } = default!;

    protected IStringLocalizerFactory StringLocalizerFactory => LazyServiceProvider.LazyGetRequiredService<IStringLocalizerFactory>();

    protected IStringLocalizer L {
        get {
            if (_localizer == null)
            {
                _localizer = CreateLocalizer();
            }

            return _localizer;
        }
    }

    private IStringLocalizer? _localizer;

    protected Type LocalizationResource {
        get => _localizationResource;
        set {
            _localizationResource = value;
            _localizer = null;
        }
    }

    private Type _localizationResource = typeof(FlexFieldsResource);

    public abstract string Name { get; }

    public abstract string DisplayName { get; }

    public abstract FlexFieldValueType? IndexValueType { get; }

    public abstract IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args);

    public abstract FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration);

    /// <summary>
    /// Default single-value decomposition: the raw value itself, when the field type is indexable, the
    /// usage is searchable, and there is a value. Multi-valued field types (Select, Tree) override this to
    /// yield one per selected value.
    /// </summary>
    public virtual IEnumerable<object> GetSearchableValues(FlexFieldValue field)
    {
        if (IndexValueType == null || !field.Searchable || field.Value == null)
        {
            yield break;
        }

        yield return field.Value;
    }

    /// <summary>
    /// Reads a multi-valued field's bag value as a list of strings, whatever shape the round trip left it in.
    /// <para>
    /// A value bag is a <c>Dictionary&lt;string, object&gt;</c>, so what comes back out of one depends on who
    /// put it back: a JSON deserializer that does not infer types leaves a <see cref="JsonElement"/>, a
    /// document driver leaves a list of boxed elements rather than a <c>List&lt;string&gt;</c>, and an entity
    /// still in memory holds whatever the caller set. A field type that assumes one of those shapes works
    /// only on the storage it was written against - which is not something a field type is supposed to know.
    /// </para>
    /// <para>
    /// Lenient rather than strict, deliberately: a single scalar reads as a one-element list, and a null or
    /// absent value as an empty one. A field type's job here is to decompose a value for indexing and
    /// validation, not to be the place a storage-shape surprise turns into a cast exception.
    /// </para>
    /// </summary>
    protected static List<string> ReadStringList(object? value)
    {
        switch (value)
        {
            case null:
                return new List<string>();
            case List<string> list:
                return list;
            // Before IEnumerable: a string is a sequence of chars, and "abc" is one value, not three.
            case string single:
                return new List<string> { single };
            case JsonElement element:
                return ReadStringList(element);
            case IEnumerable<string> strings:
                return strings.ToList();
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

    private static List<string> ReadStringList(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => new List<string>(),
            JsonValueKind.Array => element.EnumerateArray().Select(ReadElement).ToList(),
            _ => new List<string> { ReadElement(element) }
        };

        static string ReadElement(JsonElement item)
        {
            return item.ValueKind == JsonValueKind.String ? item.GetString()! : item.ToString();
        }
    }

    protected virtual IStringLocalizer CreateLocalizer()
    {
        return StringLocalizerFactory.Create(LocalizationResource);
    }
}
