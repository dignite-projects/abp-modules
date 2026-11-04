using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

public enum TestPageStatus
{
    Draft,
    Published
}

/// <summary>
/// What a tool for a Chinese-language site returns: text with non-ASCII characters and HTML, an enum, and
/// a null that is meant to be left out.
/// </summary>
public class TestPageDto
{
    public string Title { get; set; } = default!;

    public string Html { get; set; } = default!;

    public TestPageStatus Status { get; set; }

    public string? Summary { get; set; }
}

public static class TestJsonData
{
    public const string Title = "开始使用 Dignite";

    /// <summary>Every character the SDK's default encoder escapes: <c>&lt; &gt; " ' &amp;</c> next to CJK text.</summary>
    public const string Html = "<div class=\"x\">Tom & Jerry's 主页</div>";

    public static TestPageDto CreatePage()
    {
        return new TestPageDto
        {
            Title = Title,
            Html = Html,
            Status = TestPageStatus.Published
        };
    }
}

/// <summary>
/// A type the SDK knows nothing about, written by a converter that only the host registers - so a tool that
/// returns it shows whether the host's <see cref="AbpMcpServerOptions.SerializerOptions"/> reached the tool.
/// </summary>
public readonly record struct TestStamp(string Value);

public class TestStampConverter : JsonConverter<TestStamp>
{
    public const string Prefix = "stamp:";

    public override TestStamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new TestStamp(reader.GetString()!.Substring(Prefix.Length));
    }

    public override void Write(Utf8JsonWriter writer, TestStamp value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Prefix + value.Value);
    }
}

[McpServerToolType]
public class TestJsonTools : ITransientDependency
{
    [McpServerTool(Name = "test_get_page", ReadOnly = true)]
    [Description("Returns a page as a DTO.")]
    public virtual TestPageDto GetPage() => TestJsonData.CreatePage();

    [McpServerTool(Name = "test_get_stamp", ReadOnly = true)]
    [Description("Returns a value only the host's serializer options can write.")]
    public virtual TestStamp GetStamp() => new("ok");
}

/// <summary>
/// An enum the host reads in snake_case (<c>dry_run</c>) with a converter of its own; the SDK's enum converter
/// only reads <c>DryRun</c>. It is what a prompt takes as an argument to show whether the host's
/// <see cref="AbpMcpServerOptions.SerializerOptions"/> reached it. (A type with a custom converter of its own
/// would do too, but the SDK cannot build a prompt or a resource from the bare <c>true</c> schema such a type
/// gets; an enum converter gets an ordinary string schema.)
/// </summary>
public enum TestRunMode
{
    Normal,
    DryRun
}

[McpServerPromptType]
public class TestPrompts : ITransientDependency
{
    [McpServerPrompt(Name = "test_describe_mode")]
    [Description("Takes an argument only the host's serializer options can read.")]
    public virtual string DescribeMode(TestRunMode mode) => "mode is " + mode;
}
