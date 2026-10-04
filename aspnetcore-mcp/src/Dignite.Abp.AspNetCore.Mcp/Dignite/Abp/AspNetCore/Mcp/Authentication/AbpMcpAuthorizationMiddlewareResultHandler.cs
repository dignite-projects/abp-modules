using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
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
            return;
        }

        await Inner.HandleAsync(next, context, policy, authorizeResult);
    }
}
