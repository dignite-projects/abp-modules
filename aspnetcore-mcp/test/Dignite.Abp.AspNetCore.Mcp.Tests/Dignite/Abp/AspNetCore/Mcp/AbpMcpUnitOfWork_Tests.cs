using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Errors;
using Dignite.Abp.AspNetCore.Mcp.Uow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Volo.Abp.AspNetCore.Uow;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// A tool call's changes are saved before its result is written - so a failed save is still reported as
/// the call's outcome - and every failure reported as a result is logged, as the HTTP API would log it.
/// </summary>
public class AbpMcpUnitOfWork_Tests : AbpMcpIntegratedTestBase
{
    [Fact]
    public async Task Should_Save_A_Tool_Calls_Changes_In_A_Transactional_Unit_Of_Work()
    {
        var marker = Guid.NewGuid().ToString();
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_write", Arguments(marker));

        result.IsError.ShouldNotBe(true);
        var record = GetRequiredService<TestUnitOfWorkProbe>().Get(marker);
        record.Saved.ShouldBeTrue();
        record.Committed.ShouldBeTrue();
        record.RolledBack.ShouldBeFalse();
        record.IsTransactional.ShouldBeTrue();

        // The unit-of-work middleware reserved nothing for the MCP request (IgnoredUrls), as it does not for a hub.
        record.InsideAReservedUnitOfWork.ShouldBeFalse();
    }

    [Fact]
    public void Should_Keep_The_Unit_Of_Work_Middleware_Off_Every_Mcp_Endpoint()
    {
        var server = new AbpMcpServerOptions { RoutePattern = "mcp/" };
        server.Endpoints.Add(new AbpMcpEndpointOptions("/admin-mcp"));
        server.Endpoints.Add(new AbpMcpEndpointOptions("/t/{tenant}/mcp"));
        server.Endpoints.Add(new AbpMcpEndpointOptions("{tenant}/mcp"));
        var unitOfWorkOptions = new AbpAspNetCoreUnitOfWorkOptions();
        unitOfWorkOptions.IgnoredUrls.Add("/mcp");

        new AbpMcpUnitOfWorkOptionsSetup(Options.Create(server)).PostConfigure(null, unitOfWorkOptions);

        // A route starting with a parameter has no prefix to ignore; one already listed is not listed twice.
        unitOfWorkOptions.IgnoredUrls.ShouldBe(new[] { "/mcp", "/admin-mcp", "/t" });
        GetRequiredService<IOptions<AbpAspNetCoreUnitOfWorkOptions>>().Value.IgnoredUrls.ShouldContain("/mcp");
    }

    /// <summary>
    /// The regression: without the filter, the save ran in the unit-of-work middleware after the result
    /// was already on the wire - the client got a cut-off stream instead of an error, and nothing was
    /// rolled back.
    /// </summary>
    [Fact]
    public async Task Should_Report_A_Failed_Save_As_The_Calls_Error_And_Roll_Back()
    {
        var marker = Guid.NewGuid().ToString();
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_write", Arguments(marker, failOnSave: true));

        result.IsError.ShouldBe(true);
        GetError(result).GetProperty("code").GetString().ShouldBe(TestDatabaseApi.SaveFailedCode);
        var record = GetRequiredService<TestUnitOfWorkProbe>().Get(marker);
        record.Saved.ShouldBeFalse();
        record.RolledBack.ShouldBeTrue();

        // The connection survived: the same client can carry on.
        (await client.CallToolAsync("test_echo", new Dictionary<string, object?> { ["text"] = "still here" }))
            .Content.OfType<TextContentBlock>().Single().Text.ShouldBe("still here");
    }

    /// <summary>
    /// The regression: the call only saved, and the transaction was committed by the unit-of-work middleware
    /// once the request ended. A client that closes the stream as soon as it has read the result (Codex does)
    /// aborts the request first, the commit is cancelled, and the change it was told succeeded is rolled back.
    /// </summary>
    [Fact]
    public async Task Should_Commit_Before_The_Result_Reaches_A_Client_That_Leaves_At_Once()
    {
        var marker = Guid.NewGuid().ToString();
        var record = GetRequiredService<TestUnitOfWorkProbe>().Get(marker);

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "tools/call",
                    @params = new { name = "test_write", arguments = Arguments(marker) }
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(TestAuthenticationHandler.UserHeader, Guid.NewGuid().ToString());
        request.Headers.Add(TestResponseHold.Header, marker);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        bool committedWhenTheResultArrived;
        using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
        {
            response.IsSuccessStatusCode.ShouldBeTrue();
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
            string? line;
            while ((line = await reader.ReadLineAsync()) != null && !line.StartsWith("data:", StringComparison.Ordinal))
            {
            }

            line.ShouldNotBeNull();
            line.ShouldContain("written");
            committedWhenTheResultArrived = record.Committed;
        } // the client leaves without reading the rest of the response

        // Only now may the request end - after the client has gone, as with a real client that leaves at once.
        GetRequiredService<TestResponseHold>().Release(marker);

        committedWhenTheResultArrived.ShouldBeTrue();

        // And nothing that runs after the client has gone undoes it.
        await Task.Delay(500);
        record.Committed.ShouldBeTrue();
        record.RolledBack.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Roll_Back_When_The_Tool_Itself_Fails()
    {
        var marker = Guid.NewGuid().ToString();
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_write", Arguments(marker, failInTool: true));

        result.IsError.ShouldBe(true);
        var record = GetRequiredService<TestUnitOfWorkProbe>().Get(marker);
        record.Saved.ShouldBeFalse();
        record.RolledBack.ShouldBeTrue();
    }

    /// <summary>
    /// The client is told only that an internal error occurred, so the server log is the one place the
    /// cause can be found - it must be there.
    /// </summary>
    [Fact]
    public async Task Should_Log_An_Unexpected_Failure_It_Reports_As_A_Result()
    {
        var marker = Guid.NewGuid().ToString();
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_fail_unexpected", new Dictionary<string, object?> { ["marker"] = marker });

        result.IsError.ShouldBe(true);
        GetError(result).GetProperty("message").GetString()!.ShouldNotContain(marker);
        GetRequiredService<TestLoggerProvider>().Entries.ShouldContain(entry =>
            entry.Category == typeof(McpToolErrorFilter).FullName &&
            entry.Level == LogLevel.Error &&
            entry.Exception != null &&
            entry.Exception.Message.Contains(marker));
    }

    /// <summary>A business error is the caller's to fix: logged as a warning, as over HTTP.</summary>
    [Fact]
    public async Task Should_Log_A_Business_Error_As_A_Warning()
    {
        var marker = Guid.NewGuid().ToString();
        await using var client = await ConnectAsync();

        await client.CallToolAsync("test_write", Arguments(marker, failInTool: true));

        GetRequiredService<TestLoggerProvider>().Entries.ShouldContain(entry =>
            entry.Category == typeof(McpToolErrorFilter).FullName &&
            entry.Level == LogLevel.Warning &&
            entry.Exception is Volo.Abp.BusinessException);
    }

    private static Dictionary<string, object?> Arguments(string marker, bool failOnSave = false, bool failInTool = false)
    {
        return new Dictionary<string, object?>
        {
            ["marker"] = marker,
            ["failOnSave"] = failOnSave,
            ["failInTool"] = failInTool
        };
    }
}
