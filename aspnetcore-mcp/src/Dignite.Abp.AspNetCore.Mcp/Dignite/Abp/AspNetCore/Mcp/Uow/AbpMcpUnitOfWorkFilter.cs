using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp.Uow;

namespace Dignite.Abp.AspNetCore.Mcp.Uow;

/// <summary>
/// Runs every tool call, resource read and prompt in a unit of work of its own, completed - saved and
/// committed - before the result is returned, and rolled back if the call fails.
/// <para>
/// <b>Why it is needed.</b> Without it the only unit of work an MCP request has is the one
/// <c>AbpUnitOfWorkMiddleware</c> completes once the whole pipeline has returned - by which time the SDK
/// has already written the tool's result to the response stream. A save that then fails (a unique index,
/// a concurrency conflict) can no longer become an error result: the middleware throws into a response
/// that has started, and the client's connection is cut off. Completing here, inside the call, surfaces such
/// a failure while it can still be reported - <see cref="Errors.McpToolErrorFilter"/>, which wraps this
/// filter, turns it into an error result like any other exception.
/// </para>
/// <para>
/// <b>Why it commits, not only saves.</b> An MVC action's filter (<c>AbpUowActionFilter</c>) takes over the
/// unit of work the middleware reserved and only saves, leaving the commit to the middleware; an HTTP client
/// waits for the end of the response, so the commit comes before it has the result. An MCP client does not:
/// the result is one event of a stream, and a client may close the stream the moment it has read it (Codex
/// does). The request is then aborted, the middleware's commit is cancelled with it and the transaction rolls
/// back - after the client was told the call succeeded. This is the case ABP itself meets with SignalR: its
/// middleware reserves no unit of work for a hub request, because one could not be completed when the client
/// disconnects, and a hub method's changes are completed by the unit of work the method begins. So the call
/// begins its own and commits it before its result is written, and <see cref="AbpMcpUnitOfWorkOptionsSetup"/>
/// keeps the middleware from reserving one for an MCP request at all. <c>requiresNew</c>: where a reservation
/// is made anyway (a route that cannot be ignored by prefix), the call still never joins it.
/// </para>
/// <para>
/// Tool calls are transactional by default, as a POST to an MVC action would be; resource reads and prompts
/// are not, as a GET would not be. <see cref="AbpUnitOfWorkDefaultOptions"/> decides either way.
/// </para>
/// </summary>
public static class AbpMcpUnitOfWorkFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallToolFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return (request, cancellationToken) =>
            RunAsync(request.Services, isWrite: true, () => next(request, cancellationToken).AsTask(), cancellationToken);
    }

    public static McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> ReadResourceFilter(
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next)
    {
        return (request, cancellationToken) =>
            RunAsync(request.Services, isWrite: false, () => next(request, cancellationToken).AsTask(), cancellationToken);
    }

    public static McpRequestHandler<GetPromptRequestParams, GetPromptResult> GetPromptFilter(
        McpRequestHandler<GetPromptRequestParams, GetPromptResult> next)
    {
        return (request, cancellationToken) =>
            RunAsync(request.Services, isWrite: false, () => next(request, cancellationToken).AsTask(), cancellationToken);
    }

    private static async ValueTask<TResult> RunAsync<TResult>(
        IServiceProvider? services,
        bool isWrite,
        Func<Task<TResult>> next,
        CancellationToken cancellationToken)
    {
        // Outside a request (a unit test invoking the pipeline directly) there is no unit of work to manage.
        var unitOfWorkManager = services?.GetService<IUnitOfWorkManager>();
        if (unitOfWorkManager == null)
        {
            return await next();
        }

        var options = new AbpUnitOfWorkOptions
        {
            IsTransactional = services!.GetRequiredService<IOptions<AbpUnitOfWorkDefaultOptions>>().Value
                .CalculateIsTransactional(autoValue: isWrite)
        };

        // Its own unit of work, even inside the one AbpUnitOfWorkMiddleware reserved for the request: see the
        // class remarks. Completed with the call's token, so a call the client cancels before its result is
        // written is rolled back rather than committed unseen.
        using (var unitOfWork = unitOfWorkManager.Begin(options, requiresNew: true))
        {
            TResult result;
            try
            {
                result = await next();
            }
            catch
            {
                await RollbackAsync(unitOfWork);
                throw;
            }

            try
            {
                await unitOfWork.CompleteAsync(cancellationToken);
            }
            catch
            {
                await RollbackAsync(unitOfWork);
                throw;
            }

            return result;
        }
    }

    /// <summary>
    /// Rolled back without the request's token: a call that failed because the client went away must still
    /// discard what it changed, and a cancelled token would abort the rollback itself.
    /// </summary>
    private static Task RollbackAsync(IUnitOfWork unitOfWork)
    {
        return unitOfWork.RollbackAsync(CancellationToken.None);
    }
}
