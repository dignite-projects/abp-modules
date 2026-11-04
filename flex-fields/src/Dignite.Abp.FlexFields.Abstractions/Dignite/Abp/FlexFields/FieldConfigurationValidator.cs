using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// The default <see cref="IFieldConfigurationValidator"/>.
/// <para>
/// <b>It accepts only what the kernel's own reader can read.</b> A configuration value is read by
/// <see cref="FieldConfigurationDictionaryExtensions.GetConfiguration{TConfiguration}"/>, which converts
/// through a string: a number may arrive as <c>"10"</c> (the Angular <c>Number</c> editor sends its
/// <c>Number.Min</c> as the text it was typed as), a date as <c>"2026-01"</c>. Refusing those would break the
/// editors that write them, so they are accepted - and what the reader cannot read, such as an enum member by
/// name, is refused, because storing it would only move the failure to the first read. The check is a little
/// stricter than the reader where the reader is lenient to no purpose (a number for a string key).
/// </para>
/// <para>
/// A value reaches this class in whatever shape its source left it in (<c>JsonElement</c>, a boxed number, a
/// <c>List&lt;object&gt;</c>, a typed object set from code), so each is first brought to JSON, the one shape
/// they all have in common.
/// </para>
/// </summary>
public class FieldConfigurationValidator : IFieldConfigurationValidator, ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The longest stretch of a rejected value that a message quotes back.</summary>
    protected const int MaxQuotedLength = 80;

    /// <summary>The member name of a problem, <c>Configuration[key]</c>, as model binding names a dictionary entry.</summary>
    protected const string MemberNamePrefix = "Configuration";

    protected IFieldTypeResolver FieldTypeResolver { get; }

    protected IStringLocalizer<FlexFieldsResource> L { get; }

    public FieldConfigurationValidator(
        IFieldTypeResolver fieldTypeResolver,
        IStringLocalizer<FlexFieldsResource> localizer)
    {
        FieldTypeResolver = fieldTypeResolver;
        L = localizer;
    }

    public virtual IReadOnlyList<ValidationResult> Validate(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        FieldConfigurationDictionary? previous = null)
    {
        var errors = new List<ValidationResult>();
        var fieldTypes = FieldTypeResolver.GetAll();

        var fieldType = fieldTypes.FirstOrDefault(ft => ft.Name == fieldTypeName);
        if (fieldType != null)
        {
            AppendErrors(errors, fieldType, configuration ?? new FieldConfigurationDictionary(), previous, fieldTypes, depth: 1);
        }

        return errors;
    }

    public virtual void Check(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        FieldConfigurationDictionary? previous = null)
    {
        var errors = Validate(fieldTypeName, configuration, previous);
        if (errors.Count == 0)
        {
            return;
        }

        var message = L["Configuration:Invalid", fieldTypeName, string.Join(" ", errors.Select(error => error.ErrorMessage))];
        throw new AbpValidationException(message, errors.ToList());
    }

    protected virtual void AppendErrors(
        List<ValidationResult> errors,
        IFieldType fieldType,
        FieldConfigurationDictionary configuration,
        FieldConfigurationDictionary? previous,
        IReadOnlyList<IFieldType> fieldTypes,
        int depth)
    {
        var ownErrors = AppendKeyErrors(errors, fieldType, configuration, previous);

        // The inline fields of a composite type are the type's to declare whether or not it declares its keys,
        // and a problem with its own keys does not hide them: every problem is reported at once. The depth is
        // bounded by the same limit the nesting check enforces, because this is the first thing to walk a
        // configuration a client has just sent.
        if (fieldType is ICompositeFieldType composite && depth <= CompositeFieldNesting.MaxDepth)
        {
            AppendInlineFieldErrors(errors, composite, configuration, previous, fieldTypes, depth, ownErrors > 0);
        }
    }

    /// <summary>Checks the keys of the field type itself; returns how many problems it found.</summary>
    protected virtual int AppendKeyErrors(
        List<ValidationResult> errors,
        IFieldType fieldType,
        FieldConfigurationDictionary configuration,
        FieldConfigurationDictionary? previous)
    {
        var keys = fieldType.ConfigurationKeys;
        if (keys == null)
        {
            return 0;
        }

        var errorCountBefore = errors.Count;

        foreach (var (name, value) in configuration)
        {
            if (IsUnchanged(name, value, previous))
            {
                continue;
            }

            var key = keys.FirstOrDefault(k => k.Name == name);
            if (key == null)
            {
                errors.Add(Error(name, UnknownKeyMessage(fieldType, keys, name)));
                continue;
            }

            if (!TryToElement(value, out var element))
            {
                errors.Add(Error(name, InvalidValueMessage(key, value?.GetType().Name ?? string.Empty)));
                continue;
            }

            if (IsUnset(key, element) || IsValid(key, element))
            {
                continue;
            }

            errors.Add(Error(name, InvalidValueMessage(key, Quote(element))));
        }

        foreach (var key in keys.Where(k => k.Required))
        {
            // Missing, and not something this edit took away.
            if (!IsSet(configuration, key) && (previous == null || IsSet(previous, key)))
            {
                errors.Add(Error(key.Name, L["Configuration:Required", key.Name, fieldType.Name]));
            }
        }

        return errors.Count - errorCountBefore;
    }

    protected virtual void AppendInlineFieldErrors(
        List<ValidationResult> errors,
        ICompositeFieldType composite,
        FieldConfigurationDictionary configuration,
        FieldConfigurationDictionary? previous,
        IReadOnlyList<IFieldType> fieldTypes,
        int depth,
        bool ownKeysHadErrors)
    {
        if (!TryGetInlineFields(composite, configuration, out var inlineFields))
        {
            // A configuration the type cannot read is a problem to report, unless the type's own keys have
            // already named it, or it was just as unreadable before this edit and is not this edit's doing.
            if (!ownKeysHadErrors && (previous == null || TryGetInlineFields(composite, previous, out _)))
            {
                errors.Add(new ValidationResult(L["Configuration:Unreadable", composite.Name], new[] { MemberNamePrefix }));
            }

            return;
        }

        // An inline field exactly as it was before is the stored definition's, not the client's.
        var unchanged = previous != null && TryGetInlineFields(composite, previous, out var previousFields)
            ? previousFields.Select(Signature).OfType<string>().ToHashSet()
            : new HashSet<string>();

        foreach (var inlineField in inlineFields)
        {
            if (Signature(inlineField) is { } signature && unchanged.Contains(signature))
            {
                continue;
            }

            var inlineType = fieldTypes.FirstOrDefault(ft => ft.Name == inlineField.FieldTypeName);
            if (inlineType == null)
            {
                // Nothing else catches this at the time a definition is saved: InlineFieldValidator reports
                // it only when a value is validated, and a host cannot see into the configuration to ask.
                errors.Add(new ValidationResult(
                    L["Configuration:UnknownInlineFieldType", inlineField.Name, inlineField.FieldTypeName, RegisteredNames(fieldTypes)],
                    new[] { MemberNamePrefix }));
                continue;
            }

            var inlineErrors = new List<ValidationResult>();
            AppendErrors(inlineErrors, inlineType, inlineField.Configuration ?? new FieldConfigurationDictionary(), previous: null, fieldTypes, depth + 1);

            foreach (var inlineError in inlineErrors)
            {
                errors.Add(new ValidationResult(
                    L["Configuration:InlineField", inlineField.Name, inlineField.FieldTypeName, inlineError.ErrorMessage ?? string.Empty],
                    new[] { MemberNamePrefix }));
            }
        }
    }

    protected virtual ValidationResult Error(string keyName, string message)
    {
        return new ValidationResult(message, new[] { $"{MemberNamePrefix}[{keyName}]" });
    }

    protected virtual string UnknownKeyMessage(IFieldType fieldType, IReadOnlyList<FieldConfigurationKey> keys, string name)
    {
        if (keys.Count == 0)
        {
            return L["Configuration:UnknownKeyNoneAccepted", name, fieldType.Name];
        }

        var message = L["Configuration:UnknownKey", name, fieldType.Name, string.Join(", ", keys.Select(key => $"'{key.Name}'"))].Value;

        // Keys are stored as written, so a key is its exact spelling. Naming the one that differs only in
        // case saves a client the guess.
        var sameKeyOtherCase = keys.FirstOrDefault(key => string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase));
        return sameKeyOtherCase == null
            ? message
            : message + " " + L["Configuration:UnknownKeyCaseHint", sameKeyOtherCase.Name];
    }

    protected virtual string InvalidValueMessage(FieldConfigurationKey key, string received)
    {
        return L["Configuration:InvalidValue", key.Name, ExpectedValue(key), received];
    }

    protected virtual string ExpectedValue(FieldConfigurationKey key)
    {
        return key.ValueKind switch
        {
            FieldConfigurationValueKind.String => L["Configuration:Value:String"],
            FieldConfigurationValueKind.Integer => L["Configuration:Value:Integer"],
            FieldConfigurationValueKind.Number => L["Configuration:Value:Number"],
            FieldConfigurationValueKind.Boolean => L["Configuration:Value:Boolean"],
            FieldConfigurationValueKind.DateTime => L["Configuration:Value:DateTime"],
            FieldConfigurationValueKind.Guid => L["Configuration:Value:Guid"],
            FieldConfigurationValueKind.Enum => L["Configuration:Value:Enum", key.DescribeEnumMembers()],
            FieldConfigurationValueKind.Array => key.ValueType == null
                ? L["Configuration:Value:Array"]
                : L["Configuration:Value:ArrayShaped", key.Shape],
            _ => key.ValueType == null
                ? L["Configuration:Value:Object"]
                : L["Configuration:Value:ObjectShaped", key.Shape]
        };
    }

    /// <summary>
    /// Unset is "as if the key were not there": <c>null</c>, and for a key declared
    /// <see cref="FieldConfigurationKey.EmptyStringIsUnset"/> also an empty string. A required string key counts
    /// an empty string as unset too - a container name of <c>""</c> names nothing.
    /// </summary>
    protected virtual bool IsUnset(FieldConfigurationKey key, JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(element.GetString()))
        {
            return false;
        }

        return key.ValueKind == FieldConfigurationValueKind.String ? key.Required : key.EmptyStringIsUnset;
    }

    protected virtual bool IsValid(FieldConfigurationKey key, JsonElement element)
    {
        var text = element.ValueKind == JsonValueKind.String ? element.GetString()!.Trim() : string.Empty;

        switch (key.ValueKind)
        {
            case FieldConfigurationValueKind.String:
                return element.ValueKind == JsonValueKind.String;

            case FieldConfigurationValueKind.Integer:
                return element.ValueKind == JsonValueKind.Number
                    ? element.TryGetInt32(out _)
                    : element.ValueKind == JsonValueKind.String
                      && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            case FieldConfigurationValueKind.Number:
                return element.ValueKind == JsonValueKind.Number
                    ? element.TryGetDecimal(out _)
                    : element.ValueKind == JsonValueKind.String
                      && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

            case FieldConfigurationValueKind.Boolean:
                return element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    || (element.ValueKind == JsonValueKind.String && bool.TryParse(text, out _));

            case FieldConfigurationValueKind.DateTime:
                return element.ValueKind == JsonValueKind.String
                       && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

            case FieldConfigurationValueKind.Guid:
                return element.ValueKind == JsonValueKind.String && Guid.TryParse(text, out _);

            case FieldConfigurationValueKind.Enum:
                // By number only: the reader casts a number to the enum and has no reading of a name.
                return element.ValueKind == JsonValueKind.Number
                       && element.TryGetInt32(out var member)
                       && Enum.IsDefined(key.ValueType!, member);

            case FieldConfigurationValueKind.Array:
                return element.ValueKind == JsonValueKind.Array && ReadsAs(element, key.ValueType);

            default:
                return element.ValueKind == JsonValueKind.Object && ReadsAs(element, key.ValueType);
        }
    }

    private bool IsSet(FieldConfigurationDictionary configuration, FieldConfigurationKey key)
    {
        return configuration.TryGetValue(key.Name, out var value)
               && TryToElement(value, out var element)
               && !IsUnset(key, element);
    }

    /// <summary>Whether the key was already there with this very value: not something the client changed.</summary>
    private static bool IsUnchanged(string name, object? value, FieldConfigurationDictionary? previous)
    {
        return previous != null
               && previous.TryGetValue(name, out var before)
               && TryCanonicalize(value, out var now)
               && TryCanonicalize(before, out var then)
               && now == then;
    }

    /// <summary>
    /// Whether the value deserializes into <paramref name="type"/> - exactly the fallback the reader takes
    /// for a collection or an object. An array of strings where an array of options was meant fails here,
    /// not at the first read of the field.
    /// </summary>
    private static bool ReadsAs(JsonElement element, Type? type)
    {
        if (type == null)
        {
            return true;
        }

        try
        {
            JsonSerializer.Deserialize(element, type, JsonOptions);
            return true;
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return false;
        }
    }

    private static bool TryToElement(object? value, out JsonElement element)
    {
        switch (value)
        {
            case null:
                element = default;
                return true;
            case JsonElement jsonElement:
                element = jsonElement;
                return true;
            default:
                try
                {
                    element = JsonSerializer.SerializeToElement(value, JsonOptions);
                    return true;
                }
                catch (Exception exception) when (IsReadFailure(exception))
                {
                    element = default;
                    return false;
                }
        }
    }

    /// <summary>The value as compact JSON, so that two values written differently but meaning the same compare equal.</summary>
    private static bool TryCanonicalize(object? value, out string canonical)
    {
        canonical = "null";
        if (value == null)
        {
            return true;
        }

        try
        {
            canonical = JsonSerializer.Serialize(value, JsonOptions);
            return true;
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return false;
        }
    }

    /// <summary>An inline field, name and type and configuration, as one comparable string; null when it cannot be written out.</summary>
    private static string? Signature(InlineFieldDefinition inlineField)
    {
        return TryCanonicalize(inlineField.Configuration, out var configuration)
            ? string.Join("\n", inlineField.Name, inlineField.FieldTypeName, configuration)
            : null;
    }

    private static bool TryGetInlineFields(
        ICompositeFieldType composite,
        FieldConfigurationDictionary configuration,
        out List<InlineFieldDefinition> inlineFields)
    {
        try
        {
            inlineFields = composite.GetInlineFields(configuration).ToList();
            return true;
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            inlineFields = new List<InlineFieldDefinition>();
            return false;
        }
    }

    /// <summary>What reading or writing out a configuration value throws when the value is not what it was taken to be.</summary>
    private static bool IsReadFailure(Exception exception)
    {
        return exception is JsonException or NotSupportedException or InvalidOperationException;
    }

    private static string RegisteredNames(IReadOnlyList<IFieldType> fieldTypes)
    {
        return string.Join(", ", fieldTypes.Select(fieldType => $"'{fieldType.Name}'"));
    }

    private static string Quote(JsonElement element)
    {
        var raw = element.GetRawText();
        return raw.Length <= MaxQuotedLength ? raw : raw[..MaxQuotedLength] + "...";
    }
}
