using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.AspNetCore.Authentication;

namespace Dignite.Abp.AspNetCore.Mcp.Authentication;

/// <summary>
/// Answers an unauthenticated request to the MCP endpoint with the MCP authentication scheme's challenge -
/// <c>401</c> plus <c>WWW-Authenticate: Bearer resource_metadata="…"</c>, which is how an MCP client
/// discovers where to authenticate (RFC 9728). Every other case goes to the handler it decorates.
/// <para>
/// <b>Why the challenge is taken over here instead of naming the MCP scheme on the endpoint's policy.</b>
/// A policy that names a scheme makes ASP.NET Core's <c>PolicyEvaluator</c> authenticate through that
/// scheme and assign the result to <c>HttpContext.User</c>. With the MCP scheme forwarding to the host's
/// bearer handler, that replaces the principal ABP's dynamic claims middleware has already refreshed with
/// the raw token principal - so a role removed or a user disabled after the token was issued would still
/// pass on MCP requests until the token expired. Taking over only the challenge keeps authentication on
/// the default policy, with the principal the rest of the pipeline sees.
/// </para>
/// <para>
/// Only a challenge is redirected. A <c>403</c> (authenticated but not allowed) keeps the decorated
/// handler's behaviour, and so does every endpoint without <see cref="AbpMcpEndpointMetadata"/> - an MVC
/// host's pages still redirect to its login page.
/// </para>
/// </summary>
public class AbpMcpAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    protected IAuthorizationMiddlewareResultHandler Inner { get; }

    public AbpMcpAuthorizationMiddlewareResultHandler(IAuthorizationMiddlewareResultHandler inner)
    {
        Inner = inner;
    }

    public virtual async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged &&
            !authorizeResult.Forbidden &&
            context.GetEndpoint()?.Metadata.GetMetadata<AbpMcpEndpointMetadata>() != null)
        {
            await context.ChallengeAsync(McpAuthenticationDefaults.AuthenticationScheme);
            AddScopeToChallenge(context);
            return;
        }

        await Inner.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Adds <c>scope="…"</c> to the <c>WWW-Authenticate</c> challenge the SDK wrote, with the scopes the resource
    /// metadata advertises (<see cref="ProtectedResourceMetadata.ScopesSupported"/>).
    /// <para>
    /// The SDK's challenge carries only <c>resource_metadata</c>. The MCP authorization specification has a
    /// client take its scopes from the challenge first, then from the resource metadata's <c>scopes_supported</c>,
    /// and some clients (older builds of CLI clients among them) that find nothing in the challenge fall back to the
    /// authorization server's own list of every scope it knows, which the server then refuses. Saying the scope
    /// where a client looks first leaves it nothing to guess.
    /// </para>
    /// <para>
    /// Nothing is added when no scope is advertised, when the challenge already has one, or when a scope is not
    /// a valid token (a quoted-string would have to escape it). Only a <c>Bearer</c> challenge that names
    /// <c>resource_metadata</c>, which is the MCP one, is touched.
    /// </para>
    /// </summary>
    protected virtual void AddScopeToChallenge(HttpContext context)
    {
        var scopes = context.RequestServices
            .GetService<IOptionsMonitor<McpAuthenticationOptions>>()
            ?.Get(McpAuthenticationDefaults.AuthenticationScheme)
            .ResourceMetadata?.ScopesSupported;

        if (scopes == null || scopes.Count == 0 || scopes.Any(scope => !IsScopeToken(scope)))
        {
            return;
        }

        var challenges = context.Response.Headers.WWWAuthenticate;
        if (challenges.Count == 0)
        {
            return;
        }

        var scopeParameter = $"scope=\"{string.Join(' ', scopes)}\"";
        context.Response.Headers.WWWAuthenticate = new StringValues(challenges
            .Select(challenge => IsMcpChallenge(challenge) ? $"{challenge}, {scopeParameter}" : challenge)
            .ToArray());
    }

    private static bool IsMcpChallenge(string? challenge)
    {
        return challenge != null &&
               challenge.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) &&
               challenge.Contains("resource_metadata=", StringComparison.OrdinalIgnoreCase) &&
               !challenge.Contains("scope=", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>RFC 6749 §3.3: one or more printable characters, none of them a space, a quote or a backslash.</summary>
    private static bool IsScopeToken(string? scope)
    {
        return !string.IsNullOrEmpty(scope) && scope.All(c => c is >= '!' and <= '~' && c != '"' && c != '\\');
    }
}
