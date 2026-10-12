using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp.Authentication;

/// <summary>Marks that <c>AddAbpMcpAuthenticationDiscovery</c> has run.</summary>
internal sealed class AbpMcpAuthenticationDiscoveryMarker
{
}

/// <summary>
/// The startup half of <c>AddAbpMcpAuthenticationDiscovery</c>: its challenge handler is one
/// <see cref="IAuthorizationMiddlewareResultHandler"/> registration, and anything registered after it replaces
/// it without an error. The MCP endpoint would then answer an unauthenticated request with the host's default
/// challenge - a login-page redirect in an MVC host - which no MCP client can follow. Detected here instead.
/// </summary>
internal static class AbpMcpAuthenticationDiscoveryChecker
{
    public static void EnsureChallengeHandlerIsInstalled(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<AbpMcpAuthenticationDiscoveryMarker>() == null)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService<IAuthorizationMiddlewareResultHandler>();
        if (handler is not AbpMcpAuthorizationMiddlewareResultHandler)
        {
            throw new AbpException(
                $"AddAbpMcpAuthenticationDiscovery's challenge handler was replaced by {handler?.GetType().FullName ?? "nothing"}, " +
                "registered after it, so unauthenticated MCP requests would get the host's default challenge instead of " +
                "the 401 an MCP client needs. Register that IAuthorizationMiddlewareResultHandler before calling " +
                "AddAbpMcpAuthenticationDiscovery, which then wraps it.");
        }
    }
}
