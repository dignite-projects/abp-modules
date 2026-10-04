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
/// Gives every tool call, resource read and prompt the unit-of-work handling an MVC action gets from ABP's
/// <c>AbpUowActionFilter</c>: the changes are saved before the result is returned, and rolled back if the
/// call fails.
/// <para>
/// <b>Why it is needed.</b> Without it the only unit of work an MCP request has is the one
/// <c>AbpUnitOfWorkMiddleware</c> completes once the whole pipeline has returned - by which time the SDK
/// has already written the tool's result to the response stream. A save that then fails (a unique index,
/// a concurrency conflict) can no longer become an error result: the middleware throws into a response
/// that has started, and the client's connection is cut off. Saving here, inside the call, surfaces such a
/// failure while it can still be reported - <see cref="Errors.McpToolErrorFilter"/>, which wraps this
/// filter, turns it into an error result like any other exception.
/// </para>
/// <para>
/// <b>What it shares with MVC.</b> Like <c>AbpUowActionFilter</c> it takes over the unit of work the
/// middleware reserved, and only saves changes: committing the transaction stays with the middleware, at
/// the end of the request. Tool calls are transactional by default, as a POST to an MVC action would be;
/// resource reads and prompts are not, as a GET would not be. <see cref="AbpUnitOfWorkDefaultOptions"/>
/// decides either way.
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

        // The unit of work AbpUnitOfWorkMiddleware reserved for this request, if the host runs it.
        if (unitOfWorkManager.TryBeginReserved(UnitOfWork.UnitOfWorkReservationName, options))
        {
            TResult result;
            try
            {
                result = await next();
            }
            catch
            {
                await RollbackAsync(unitOfWorkManager);
                throw;
            }

            await SaveChangesAsync(unitOfWorkManager, cancellationToken);
            return result;
        }

        // No middleware, or the reservation was already taken: the call gets a unit of work of its own.
        using (var unitOfWork = unitOfWorkManager.Begin(options))
        {
            TResult result;
            try
            {
                result = await next();
            }
            catch
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                throw;
            }

            await unitOfWork.CompleteAsync(cancellationToken);
            return result;
        }
    }

    private static async Task SaveChangesAsync(IUnitOfWorkManager unitOfWorkManager, CancellationToken cancellationToken)
    {
        var unitOfWork = unitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            return;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Rolled back without the request's token: a call that failed because the client went away must still
    /// discard what it changed, and a cancelled token would abort the rollback itself.
    /// </summary>
    private static async Task RollbackAsync(IUnitOfWorkManager unitOfWorkManager)
    {
        var unitOfWork = unitOfWorkManager.Current;
        if (unitOfWork != null)
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
        }
    }
}
