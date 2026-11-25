using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp.Validation;

namespace Dignite.Abp.AspNetCore.Mcp.Errors;

/// <summary>
/// Recognizes the exception <c>Microsoft.Extensions.AI</c> throws when it cannot bind a tool call's JSON
/// arguments to the tool method's parameters, and turns it into the <see cref="AbpValidationException"/>
/// any other invalid input would be - naming the argument and what is wrong with it.
/// <para>
/// <b>Why this exists.</b> The MCP specification (<c>2025-11-25</c>, <c>2026-07-28</c>; SEP-1303) makes
/// "input validation errors" a <i>tool execution error</i>, reported with <c>isError: true</c> so the model
/// can read it and call again, and keeps JSON-RPC protocol errors for requests that are malformed as a
/// whole. The C# SDK does neither half of that for a tool built from a method: <c>AIFunctionFactory</c>
/// binds and invokes in one call, and whatever it throws is answered with "An error occurred invoking
/// '&lt;tool&gt;'.", which names nothing; the SDK's TypeScript, Python and Java counterparts all hand the
/// model the offending argument. Left alone, this server's error filter would classify the binder's
/// exception as <see cref="McpToolErrorKinds.Unknown"/>, and ABP's converter would reduce that to "An
/// internal error occurred during your request!".
/// </para>
/// <para>
/// <b>Recognizing it.</b> The binder throws two things: an <see cref="ArgumentException"/> whose
/// <c>ParamName</c> is <c>"arguments"</c> (a required argument is missing, or - under
/// <c>UnmappedMemberHandling.Disallow</c> - one is not a parameter), and a <see cref="JsonException"/> when a
/// value cannot be read as its parameter's type. Neither type nor <c>ParamName</c> says <i>where</i> it was
/// thrown, and a tool's own code throws <see cref="JsonException"/> as well (an upstream response that is
/// not JSON), so the stack decides: the first frame outside the platform's own assemblies must be
/// <c>Microsoft.Extensions.AI.Abstractions</c>. A tool that throws either exception itself is a tool bug
/// and stays <see cref="McpToolErrorKinds.Unknown"/>.
/// </para>
/// <para>
/// <b>Naming the argument is this module's own design, with no precedent in the C# SDK.</b> The exception
/// does not say which argument failed (each one is deserialized from its own element, so a
/// <see cref="JsonException"/> carries no argument name, and the text of the <c>ArgumentException</c> is not
/// a contract), so the request is compared with the tool's own input schema: a required argument that is
/// absent, an argument that is not a declared parameter, a value of the wrong JSON type, outside an enum, or
/// not in the declared format. It runs only after the binder has failed, and each check is at most as strict
/// as the binder - a numeric string is accepted for a number, as the SDK's serializer options do - so it
/// can only add detail to a failed call, never reject one that would have run. When the comparison finds
/// nothing to name (a constraint the schema check does not model), the call is still a validation error,
/// with a message that says only that the arguments do not match the schema. Neither the exception text nor
/// the argument values are echoed: the former carries type names, the latter may be large or private.
/// </para>
/// </summary>
internal static class McpToolArgumentBindingFailure
{
    /// <summary>A model cannot use more than a screenful of problems, and an array of wrong items has no end.</summary>
    private const int MaxProblems = 10;

    private const int MaxEnumValuesListed = 20;

    private const string NoArgumentFound = "The arguments do not match the tool's input schema.";

    /// <summary>
    /// <see langword="null"/> when <paramref name="exception"/> is not the argument binder's, so the caller
    /// reports it as it always has.
    /// </summary>
    public static AbpValidationException? TryTranslate(
        Exception exception, RequestContext<CallToolRequestParams> request)
    {
        if (!IsThrownByTheBinder(exception))
        {
            return null;
        }

        var problems = FindProblems(request);
        if (problems.Count == 0)
        {
            problems.Add(string.Empty, NoArgumentFound);
        }

        // The original stays as the inner exception, so the log still shows what the binder threw.
        var translated = new AbpValidationException(
            $"The arguments of the tool '{request.Params?.Name}' are not valid.", exception);

        foreach (var (argument, message) in problems.Items)
        {
            translated.ValidationErrors.Add(new ValidationResult(
                message, argument.Length == 0 ? Array.Empty<string>() : new[] { argument }));
        }

        return translated;
    }

    private static bool IsThrownByTheBinder(Exception exception)
    {
        if (exception is not (JsonException or ArgumentException { ParamName: "arguments" }))
        {
            return false;
        }

        var binderAssembly = typeof(AIFunction).Assembly;

        // The throw site is the top of the stack: the platform's own frames (System.Text.Json, the helper
        // that builds the exception) come first, and the first frame after them is the code that called
        // them. A frame without a declaring type is a dynamically emitted reflection stub.
        foreach (var frame in new StackTrace(exception, fNeedFileInfo: false).GetFrames())
        {
            var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
            if (assembly == null || IsPlatformAssembly(assembly))
            {
                continue;
            }

            return assembly == binderAssembly;
        }

        // No stack to read: the exception's own shape is all there is.
        return true;
    }

    private static bool IsPlatformAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return name != null && (name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib" or "netstandard");
    }

    private static Problems FindProblems(RequestContext<CallToolRequestParams> request)
    {
        var problems = new Problems();

        if (request.MatchedPrimitive is McpServerTool tool &&
            tool.ProtocolTool.InputSchema is { ValueKind: JsonValueKind.Object } schema)
        {
            var arguments = (IReadOnlyDictionary<string, JsonElement>?)request.Params?.Arguments?
                                .ToDictionary(pair => pair.Key, pair => pair.Value)
                            ?? new Dictionary<string, JsonElement>();

            CheckObject(schema, arguments, prefix: string.Empty, isRoot: true, problems);
        }

        return problems;
    }

    private static void CheckObject(
        JsonElement schema, IReadOnlyDictionary<string, JsonElement> values, string prefix, bool isRoot, Problems problems)
    {
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredName in required.EnumerateArray())
            {
                if (requiredName.ValueKind == JsonValueKind.String &&
                    requiredName.GetString() is { } name &&
                    !values.ContainsKey(name))
                {
                    var path = Join(prefix, name);
                    problems.Add(path, $"The '{path}' argument is required.");
                }
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties) &&
                            properties.ValueKind == JsonValueKind.Object;

        // An undeclared argument is only a problem in itself where unknown members are refused. The tool's
        // own arguments are named after its parameters, and one that matches none of them is the likeliest
        // reason for a missing one (the model wrote `template` for `templateName`), so a call that has
        // already failed names it. Inside an argument, an extra member is ignored by the serializer unless
        // the schema says otherwise.
        var rejectsUnknown = schema.TryGetProperty("additionalProperties", out var additional)
            ? additional.ValueKind == JsonValueKind.False
            : isRoot;

        foreach (var (name, value) in values)
        {
            var path = Join(prefix, name);

            if (hasProperties && properties.TryGetProperty(name, out var propertySchema))
            {
                CheckValue(propertySchema, value, path, problems);
            }
            else if (hasProperties && rejectsUnknown)
            {
                problems.Add(path, $"The '{path}' argument is not a parameter of this tool.");
            }

            if (problems.IsFull)
            {
                return;
            }
        }
    }

    private static void CheckValue(JsonElement schema, JsonElement value, string path, Problems problems)
    {
        // A schema that combines or references others is not modelled: it is left to the binder, which is
        // never wrong, rather than judged by a check that might be.
        if (schema.ValueKind != JsonValueKind.Object || HasCombinator(schema))
        {
            return;
        }

        if (TryGetTypes(schema, out var types) && !types.Any(type => IsOfType(type, value)))
        {
            problems.Add(path, $"The '{path}' argument must be {string.Join(" or ", types.Select(Describe))}.");
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                CheckString(schema, value, path, problems);
                break;

            case JsonValueKind.Number:
                CheckEnum(schema, value, path, problems);
                break;

            case JsonValueKind.Object when schema.TryGetProperty("properties", out _):
                var members = new Dictionary<string, JsonElement>();
                foreach (var member in value.EnumerateObject())
                {
                    members[member.Name] = member.Value;
                }

                CheckObject(schema, members, path, isRoot: false, problems);
                break;

            case JsonValueKind.Array when schema.TryGetProperty("items", out var items):
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    CheckValue(items, item, $"{path}[{index++}]", problems);
                    if (problems.IsFull)
                    {
                        return;
                    }
                }

                break;
        }
    }

    private static void CheckString(JsonElement schema, JsonElement value, string path, Problems problems)
    {
        var text = value.GetString() ?? string.Empty;

        if (schema.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String &&
            Formats.TryGetValue(format.GetString()!, out var declared) && !declared.IsValid(text))
        {
            problems.Add(path, $"The '{path}' argument must be {declared.Description}.");
            return;
        }

        CheckEnum(schema, value, path, problems);
    }

    private static void CheckEnum(JsonElement schema, JsonElement value, string path, Problems problems)
    {
        if (!schema.TryGetProperty("enum", out var allowed) || allowed.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        // A [Flags] enum is written as a comma-separated list of its names, which no single entry matches.
        if (value.ValueKind == JsonValueKind.String && value.GetString()!.Contains(','))
        {
            return;
        }

        var values = allowed.EnumerateArray().ToList();
        if (values.Any(entry => IsSameEnumValue(entry, value)))
        {
            return;
        }

        var listed = values.Take(MaxEnumValuesListed).Select(entry =>
            entry.ValueKind == JsonValueKind.String ? entry.GetString() : entry.GetRawText());
        var more = values.Count > MaxEnumValuesListed ? ", ..." : string.Empty;
        problems.Add(path, $"The '{path}' argument must be one of: {string.Join(", ", listed)}{more}.");
    }

    // The serializer reads an enum name without regard to case, and an enum number as the number it is.
    private static bool IsSameEnumValue(JsonElement entry, JsonElement value)
    {
        return entry.ValueKind switch
        {
            JsonValueKind.String when value.ValueKind == JsonValueKind.String =>
                string.Equals(entry.GetString(), value.GetString(), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number when value.ValueKind == JsonValueKind.Number =>
                entry.GetRawText() == value.GetRawText(),
            _ => false
        };
    }

    private static bool HasCombinator(JsonElement schema)
    {
        return schema.TryGetProperty("$ref", out _) || schema.TryGetProperty("anyOf", out _) ||
               schema.TryGetProperty("oneOf", out _) || schema.TryGetProperty("allOf", out _) ||
               schema.TryGetProperty("not", out _);
    }

    private static bool TryGetTypes(JsonElement schema, out List<string> types)
    {
        types = new List<string>();

        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            types.Add(type.GetString()!);
        }
        else if (type.ValueKind == JsonValueKind.Array)
        {
            types.AddRange(type.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString()!));
        }

        return types.Count > 0;
    }

    /// <summary>
    /// Whether <paramref name="value"/> can be a JSON Schema <paramref name="type"/> - no more strictly than
    /// the binder: its serializer options let a number be written as a string.
    /// </summary>
    private static bool IsOfType(string type, JsonElement value)
    {
        return type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number || IsNumberInString(value, integer: false),
            // The serializer reads `5.0` or `1e3` into an integer parameter no more than `"abc"`.
            "integer" => value.ValueKind == JsonValueKind.Number
                ? value.GetRawText().IndexOfAny(new[] { '.', 'e', 'E' }) < 0
                : IsNumberInString(value, integer: true),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            "object" => value.ValueKind == JsonValueKind.Object,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        };
    }

    private static bool IsNumberInString(JsonElement value, bool integer)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = value.GetString()!;
        return integer
            ? BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
            : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static string Describe(string type)
    {
        return type switch
        {
            "integer" => "an integer",
            "array" => "an array",
            "object" => "an object",
            "null" => "null",
            _ => $"a {type}"
        };
    }

    private static string Join(string prefix, string name)
    {
        return prefix.Length == 0 ? name : $"{prefix}.{name}";
    }

    /// <summary>
    /// The JSON Schema <c>format</c>s the serializer's schema exporter writes for .NET types the serializer
    /// reads from a string, each checked with the parser of the .NET type it stands for (and no more
    /// strictly than the serializer, which is stricter than these parsers about the exact form).
    /// </summary>
    private static readonly Dictionary<string, (Func<string, bool> IsValid, string Description)> Formats = new()
    {
        ["uuid"] = (text => Guid.TryParse(text, out _), "a GUID"),
        ["date-time"] = (text => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "a date and time in ISO 8601 format"),
        ["date"] = (text => DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "a date in yyyy-MM-dd format"),
        ["time"] = (text => TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "a time of day in HH:mm:ss format"),
        ["duration"] = (text => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out _), "a duration"),
        ["uri"] = (text => Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out _), "a URI")
    };

    private sealed class Problems
    {
        public List<(string Argument, string Message)> Items { get; } = new();

        public int Count => Items.Count;

        public bool IsFull => Items.Count >= MaxProblems;

        public void Add(string argument, string message)
        {
            if (!IsFull)
            {
                Items.Add((argument, message));
            }
        }
    }
}
