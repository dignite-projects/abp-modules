using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Volo.Abp.AspNetCore.Uow;

namespace Dignite.Abp.AspNetCore.Mcp.Uow;

/// <summary>
/// Keeps ABP's unit-of-work middleware off the MCP endpoints, by adding each endpoint's route to
/// <see cref="AbpAspNetCoreUnitOfWorkOptions.IgnoredUrls"/> - as the middleware itself leaves a SignalR hub request
/// alone.
/// <para>
/// <b>Why.</b> Every call commits in a unit of work of its own (<see cref="AbpMcpUnitOfWorkFilter"/>), so the one
/// the middleware reserves for the request has nothing to do - but it does not stay empty: the first repository call
/// of the request (the permission check before the tool runs) begins it through ABP's unit-of-work interceptor, with
/// a transaction on a POST. A client that closes the stream as soon as it has read the result aborts the request,
/// and the middleware's commit of that transaction then fails and is logged as an error on every such call. Without
/// the reservation, those calls get units of work of their own, completed as they return.
/// </para>
/// <para>
/// <c>IgnoredUrls</c> matches by prefix, case-insensitively, so a route is added up to its first parameter; a route
/// that starts with a parameter has no literal prefix and is not added (its calls still commit before their result
/// is written - only the logged error remains).
/// </para>
/// </summary>
public class AbpMcpUnitOfWorkOptionsSetup : IPostConfigureOptions<AbpAspNetCoreUnitOfWorkOptions>
{
    protected AbpMcpServerOptions Options { get; }

    public AbpMcpUnitOfWorkOptionsSetup(IOptions<AbpMcpServerOptions> options)
    {
        Options = options.Value;
    }

    public virtual void PostConfigure(string? name, AbpAspNetCoreUnitOfWorkOptions options)
    {
        foreach (var route in Routes())
        {
            var prefix = LiteralPrefix(route);
            if (prefix != null && !options.IgnoredUrls.Contains(prefix))
            {
                options.IgnoredUrls.Add(prefix);
            }
        }
    }

    protected virtual IEnumerable<string> Routes()
    {
        yield return Options.RoutePattern;
        foreach (var endpoint in Options.Endpoints)
        {
            yield return endpoint.RoutePattern;
        }
    }

    /// <summary><c>/mcp</c> for <c>mcp/</c>, <c>/t</c> for <c>/t/{tenant}/mcp</c>, <c>null</c> for <c>{tenant}/mcp</c>.</summary>
    protected static string? LiteralPrefix(string route)
    {
        var literal = route;
        var parameter = literal.IndexOf('{');
        if (parameter >= 0)
        {
            literal = literal.Substring(0, parameter);
        }

        literal = literal.Trim().Trim('/');
        return literal.Length == 0 ? null : "/" + literal;
    }
}
