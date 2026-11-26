using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// A call whose arguments do not fit the tool is a validation error that names the argument - not "an
/// internal error occurred" - because the model can fix it and call again (MCP specification, SEP-1303).
/// </summary>
public class AbpMcpToolArguments_Tests : AbpMcpIntegratedTestBase
{
    [Fact]
    public async Task Should_Name_A_Missing_Required_Argument()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new() { ["left"] = 1 });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).ShouldBe(new[] { "right" });
        MessageOf(error, "right").ShouldBe("The 'right' argument is required.");
    }

    /// <summary>
    /// The failure this was written for: the model wrote <c>txt</c> for <c>text</c>. The unknown name is what
    /// tells it which argument it meant.
    /// </summary>
    [Fact]
    public async Task Should_Name_A_Misspelled_Argument_Together_With_The_Missing_One()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_echo", new() { ["txt"] = "hello" });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).OrderBy(field => field).ShouldBe(new[] { "text", "txt" });
        MessageOf(error, "text").ShouldBe("The 'text' argument is required.");
        MessageOf(error, "txt").ShouldBe("The 'txt' argument is not a parameter of this tool.");
        // The model reads the text, not the payload: clients are not obliged to show structuredContent.
        TextOf(result).ShouldContain("text: The 'text' argument is required.");
    }

    [Fact]
    public async Task Should_Name_Every_Missing_Argument_When_There_Are_None()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new());

        FieldsOf(GetError(result)).ShouldBe(new[] { "left", "right" });
    }

    [Fact]
    public async Task Should_Name_An_Argument_Of_The_Wrong_Type()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new() { ["left"] = "abc", ["right"] = 1 });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).ShouldBe(new[] { "left" });
        MessageOf(error, "left").ShouldBe("The 'left' argument must be an integer.");
    }

    [Theory]
    [InlineData("5.5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{}")]
    public async Task Should_Name_An_Integer_Argument_Given_Another_Kind_Of_Value(string json)
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new()
        {
            ["left"] = JsonDocument.Parse(json).RootElement.Clone(),
            ["right"] = 1
        });

        FieldsOf(GetError(result)).ShouldBe(new[] { "left" });
    }

    /// <summary>
    /// Only a call that failed is explained, and never by a stricter reading than the binder's: the SDK's
    /// serializer options accept a number written as a string, so this call runs.
    /// </summary>
    [Fact]
    public async Task Should_Not_Refuse_A_Number_Written_As_A_String()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new() { ["left"] = "2", ["right"] = 3 });

        result.IsError.ShouldNotBe(true);
        TextOf(result).ShouldBe("5");
    }

    [Fact]
    public async Task Should_Name_An_Argument_Of_The_Wrong_Format()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_find", new() { ["id"] = "not-a-guid" });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).ShouldBe(new[] { "id" });
        MessageOf(error, "id").ShouldBe("The 'id' argument must be a GUID.");
    }

    [Fact]
    public async Task Should_Name_An_Argument_Outside_Its_Enum()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_find", new() { ["id"] = Guid.NewGuid(), ["status"] = "Archived" });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).ShouldBe(new[] { "status" });
        MessageOf(error, "status").ShouldContain("must be one of: Draft, Published");
    }

    [Fact]
    public async Task Should_Name_A_Nested_Member_Of_The_Wrong_Shape()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_book", new()
        {
            ["booking"] = JsonDocument.Parse("""{ "guests": "none" }""").RootElement.Clone()
        });

        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        FieldsOf(error).OrderBy(field => field).ShouldBe(new[] { "booking.guests", "booking.name" });
        MessageOf(error, "booking.guests").ShouldBe("The 'booking.guests' argument must be an array.");
        MessageOf(error, "booking.name").ShouldBe("The 'booking.name' argument is required.");
    }

    [Fact]
    public async Task Should_Name_An_Item_Of_An_Array_Argument()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_book", new()
        {
            ["booking"] = JsonDocument.Parse(
                """{ "name": "trip", "guests": [ { "name": "a", "age": 3 }, { "name": "b", "age": "old" } ] }""").RootElement.Clone()
        });

        FieldsOf(GetError(result)).ShouldBe(new[] { "booking.guests[1].age" });
    }

    /// <summary>
    /// Neither the exception text (which carries .NET type names) nor the value the model sent is repeated.
    /// </summary>
    [Fact]
    public async Task Should_Not_Echo_The_Exception_Or_The_Value()
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, "test_sum", new() { ["left"] = "secret-value", ["right"] = 1 });

        var everything = TextOf(result) + GetError(result).GetRawText();
        everything.ShouldNotContain("secret-value");
        everything.ShouldNotContain("System.");
        everything.ShouldNotContain("Path:");
        everything.ShouldNotContain("The arguments dictionary");
    }

    /// <summary>
    /// The call failed on the binder, which logs as the validation error it is - a warning, as an invalid
    /// request is over HTTP - not as an unexpected failure.
    /// </summary>
    [Fact]
    public async Task Should_Log_It_As_A_Warning()
    {
        await using var client = await ConnectAsync();
        var log = GetRequiredService<TestLoggerProvider>();
        log.Entries.Clear();

        await CallAsync(client, "test_sum", new() { ["left"] = "abc", ["right"] = 1 });

        var filterEntries = log.Entries.Where(entry => entry.Category == typeof(McpToolErrorFilter).FullName).ToList();
        filterEntries.ShouldNotBeEmpty();
        filterEntries.ShouldAllBe(entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>
    /// The mistake of a tool is not the caller's: an <see cref="ArgumentException"/> a tool throws for one of
    /// its own reasons is still an internal error, whatever its parameter name.
    /// </summary>
    [Theory]
    [InlineData("test_fail_own_argument_exception")]
    [InlineData("test_fail_own_arguments_exception")]
    [InlineData("test_fail_own_json_exception")]
    public async Task Should_Keep_An_Exception_Thrown_By_The_Tool_Itself_Unknown(string tool)
    {
        await using var client = await ConnectAsync();

        var result = await CallAsync(client, tool, new());

        result.IsError.ShouldBe(true);
        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("unknown");
        error.GetProperty("validationErrors").GetArrayLength().ShouldBe(0);
    }

    private static Task<CallToolResult> CallAsync(McpClient client, string tool, Dictionary<string, object?> arguments)
    {
        return client.CallToolAsync(tool, arguments).AsTask();
    }

    private static List<string> FieldsOf(JsonElement error)
    {
        return error.GetProperty("validationErrors").EnumerateArray()
            .SelectMany(validationError => validationError.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetString()!)
            .ToList();
    }

    private static string MessageOf(JsonElement error, string field)
    {
        return error.GetProperty("validationErrors").EnumerateArray()
            .Single(validationError => validationError.GetProperty("fields").EnumerateArray()
                .Any(candidate => candidate.GetString() == field))
            .GetProperty("message").GetString()!;
    }

    private static string TextOf(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }
}
