using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// What a model reads when a tool returns a DTO: the SDK writes it as JSON text with the primitive's
/// serializer options, whose default encoder turns every non-ASCII character and <c>&lt; &gt; " ' &amp;</c>
/// into <c>\uXXXX</c>. <see cref="AbpMcpServerOptions.SerializerOptions"/> is what keeps them readable.
/// </summary>
public class AbpMcpSerialization_Tests : AbpMcpIntegratedTestBase
{
    [Fact]
    public async Task Should_Return_A_Dto_Without_Escaping_Non_Ascii_Or_Markup_Characters()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_get_page");

        result.IsError.ShouldNotBe(true);
        var text = GetText(result);

        // The characters themselves ...
        text.ShouldContain("开始使用");
        text.ShouldContain("主页");
        text.ShouldContain("<div class=");
        text.ShouldContain("Tom & Jerry's");
        // ... a quote inside a JSON string is still escaped, as JSON requires, but as \" and not as " ...
        text.ShouldContain("<div class=\\\"x\\\">");
        // ... and nothing is written as a \uXXXX escape.
        text.ShouldNotContain("\\u");

        // Same value, once parsed.
        var page = JsonSerializer.Deserialize<TestPageDto>(text, McpJsonUtilities.DefaultOptions)!;
        page.Title.ShouldBe(TestJsonData.Title);
        page.Html.ShouldBe(TestJsonData.Html);
    }

    /// <summary>
    /// The options are a copy of the SDK's, so the contracts the SDK's own default gives a DTO - web
    /// naming, enums as strings, nulls left out - are unchanged. Only the escaping differs.
    /// </summary>
    [Fact]
    public async Task Should_Write_The_Same_Value_As_The_Sdk_Default_Apart_From_Escaping()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_get_page");

        var written = JsonNode.Parse(GetText(result))!;
        var sdkDefault = JsonSerializer.SerializeToNode(TestJsonData.CreatePage(), McpJsonUtilities.DefaultOptions)!;

        JsonNode.DeepEquals(written, sdkDefault).ShouldBeTrue();

        // So the equality above is not vacuous: what the default contract is, spelled out.
        written["title"]!.GetValue<string>().ShouldBe(TestJsonData.Title);
        written["status"]!.GetValue<string>().ShouldBe("Published");
        written.AsObject().ContainsKey("summary").ShouldBeFalse();
        written.AsObject().ContainsKey("Title").ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_To_A_Relaxed_Copy_Of_The_Sdk_Options()
    {
        var options = new AbpMcpServerOptions().SerializerOptions;

        options.Encoder.ShouldBeSameAs(JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
        // A copy: the SDK's shared instance is left as it was.
        options.ShouldNotBeSameAs(McpJsonUtilities.DefaultOptions);
        McpJsonUtilities.DefaultOptions.Encoder.ShouldNotBeSameAs(JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

        options.PropertyNamingPolicy.ShouldBe(McpJsonUtilities.DefaultOptions.PropertyNamingPolicy);
        options.DefaultIgnoreCondition.ShouldBe(McpJsonUtilities.DefaultOptions.DefaultIgnoreCondition);
        options.NumberHandling.ShouldBe(McpJsonUtilities.DefaultOptions.NumberHandling);
        options.TypeInfoResolver.ShouldNotBeNull();
        options.TypeInfoResolverChain.Count.ShouldBe(McpJsonUtilities.DefaultOptions.TypeInfoResolverChain.Count);
    }

    /// <summary>
    /// <c>AddTools</c> runs while services are still being registered, before the host's
    /// <c>Configure&lt;AbpMcpServerOptions&gt;</c> has run, so the options are only found out when the
    /// primitive is built. A converter the test module added to them is what writes this value.
    /// </summary>
    [Fact]
    public async Task Should_Use_The_Serializer_Options_The_Host_Configured()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_get_stamp");

        GetText(result).ShouldBe("\"" + TestStampConverter.Prefix + "ok\"");
    }

    /// <summary>
    /// A prompt, like a tool, reads its arguments with the options it was built with. A prompt writes no DTO as
    /// JSON, so an argument is what shows the options reached it: the SDK's own options read <c>DryRun</c> and
    /// not <c>dry_run</c>, which only the converter the test module added to
    /// <see cref="AbpMcpServerOptions.SerializerOptions"/> reads.
    /// <para>
    /// There is no such test for a resource. SDK 2.3.0 returns no DTO from one, converts the URI variables it
    /// passes to the method itself (an enum with <c>Enum.Parse</c>, so <c>dry_run</c> fails whatever the options
    /// say) and cannot build a resource whose argument has a custom converter, so nothing a resource does
    /// depends on these options yet.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Should_Read_A_Prompt_Argument_With_The_Serializer_Options_The_Host_Configured()
    {
        await using var client = await ConnectAsync();

        var result = await client.GetPromptAsync("test_describe_mode", new Dictionary<string, object?>
        {
            ["mode"] = "dry_run"
        });

        result.Messages.Single().Content.ShouldBeOfType<TextContentBlock>().Text.ShouldBe("mode is DryRun");
    }

    /// <summary>
    /// The relaxed encoder is safe because the text is JSON inside a content block, never HTML - and because
    /// the JSON-RPC message that carries it is written by the SDK's own options, so on the wire the HTTP
    /// response is escaped exactly as before. This is the guard on that second half: a response the browser
    /// could be made to read as markup would carry a raw <c>&lt;</c>.
    /// </summary>
    [Fact]
    public async Task Should_Keep_The_Http_Response_Escaped()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"test_get_page","arguments":{}}}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add(TestAuthenticationHandler.UserHeader, Guid.NewGuid().ToString());

        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("\\u003Cdiv");
        body.ShouldNotContain("<");
    }

    private static string GetText(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }
}
