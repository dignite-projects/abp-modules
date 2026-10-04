using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
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
        record.RolledBack.ShouldBeFalse();
        record.IsTransactional.ShouldBeTrue();
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
