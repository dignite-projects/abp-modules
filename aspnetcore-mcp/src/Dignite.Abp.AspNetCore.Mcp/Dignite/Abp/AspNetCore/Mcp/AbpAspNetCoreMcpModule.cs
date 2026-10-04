using Dignite.Abp.AspNetCore.Mcp.Authentication;
using Dignite.Abp.AspNetCore.Mcp.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Hosts the one MCP server an application has, and lets any number of modules contribute to it.
/// <para>
/// <b>Why a module of its own.</b> The C# SDK keeps exactly one MCP server per service container: every
/// <c>WithTools&lt;T&gt;()</c> call in every module lands in the same server, while server info, the
/// instructions, the transport, the endpoint and every request filter exist once. A feature module that
/// configures any of those - as <c>Dignite.Site.Mcp</c> once did - is configuring the whole server, and
/// the second such module collides with it. This module owns all of the once-per-server parts, the same
/// split ABP draws for SignalR (<c>AbpAspNetCoreSignalRModule</c> maps the hubs; feature modules only
/// supply them). Feature modules depend on this one and contribute through
/// <see cref="AbpMcpServiceCollectionExtensions.AddAbpMcpModule"/>; hosts only configure
/// <see cref="AbpMcpServerOptions"/>.
/// </para>
/// <para>
/// <b>The transport is stateless Streamable HTTP</b> (the SDK default as of the <c>2026-07-28</c>
/// protocol revision), so every MCP request is an ordinary HTTP request through the host's ordinary
/// pipeline: authentication, tenant resolution and the unit of work have all happened before a tool runs.
/// </para>
/// </summary>
[DependsOn(typeof(AbpAspNetCoreModule))]
public class AbpAspNetCoreMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Created here too, not only by the first AddAbpMcpModule call, so a host with no contributing
        // module still gets an (empty) server rather than a resolution failure.
        context.Services.GetOrAddAbpMcpModuleRegistry();
        context.Services.TryAddSingleton<AbpMcpPrimitiveCache>();

        context.Services
            .AddMcpServer()
            .WithHttpTransport()
            // Honours [Authorize] on tools, resources and prompts in two places: what the current user
            // cannot call never appears in the list results, and a call that slips through is refused.
            // ABP's AbpAuthorizationPolicyProvider resolves permission names as policies, so
            // [Authorize(MyPermissions.X)] works as-is. Called here and nowhere else: unlike the SDK's
            // other registrations it is not idempotent, and a second call runs every check twice.
            .AddAuthorizationFilters()
            // One place turns an exception into a result a model can act on - see McpToolErrorFilter.
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpToolErrorFilter.CallToolFilter));

        // ServerInfo and ServerInstructions, read from AbpMcpServerOptions once the container is built - so a
        // host sets them with a plain Configure<AbpMcpServerOptions>, no PreConfigure needed. A singleton:
        // the SDK applies it to a fresh McpServerOptions on every stateless request, and what it copies is
        // fixed after startup, so it is computed once.
        context.Services.AddSingleton<IConfigureOptions<McpServerOptions>, AbpMcpServerOptionsSetup>();

        Configure<AbpEndpointRouterOptions>(routerOptions =>
        {
            routerOptions.EndpointConfigureActions.Add(endpointContext =>
            {
                var mcpOptions = endpointContext.ScopeServiceProvider
                    .GetRequiredService<IOptions<AbpMcpServerOptions>>().Value;

                // The default authorization policy, deliberately WITHOUT naming an authentication scheme.
                // Naming one makes ASP.NET Core's PolicyEvaluator re-authenticate through it and assign
                // the result to HttpContext.User - replacing the principal that ABP's dynamic claims
                // middleware already refreshed, so role changes and revocations made after a token was
                // issued would not reach MCP requests. The MCP-specific 401 (RFC 9728 discovery) is
                // produced by AbpMcpAuthorizationMiddlewareResultHandler instead, which only takes over
                // the challenge and leaves authentication alone.
                var endpoint = endpointContext.Endpoints
                    .MapMcp(mcpOptions.RoutePattern)
                    .RequireAuthorization()
                    .WithMetadata(AbpMcpEndpointMetadata.Instance);

                // Bounds the body before the SDK buffers and deserializes it - a tool can only check its
                // own arguments after the whole request is already in memory.
                if (mcpOptions.MaxRequestBodySize.HasValue)
                {
                    endpoint.WithMetadata(new AbpMcpRequestSizeLimitMetadata(mcpOptions.MaxRequestBodySize.Value));
                }

                foreach (var convention in mcpOptions.EndpointConventions)
                {
                    convention(endpoint);
                }
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Fails startup on a tool name, prompt name or resource URI that two contributors both claim, or
        // that falls outside its module's namespace. The SDK would otherwise keep whichever registered
        // first and drop the other without a word.
        context.ServiceProvider.GetRequiredService<AbpMcpPrimitiveValidator>().Validate();

        AbpMcpAuthenticationDiscoveryChecker.EnsureChallengeHandlerIsInstalled(context.ServiceProvider);
    }
}
