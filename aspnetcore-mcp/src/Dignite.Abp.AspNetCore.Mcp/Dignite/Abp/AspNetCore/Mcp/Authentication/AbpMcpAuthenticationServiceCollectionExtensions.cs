using System;
using System.Linq;
using Dignite.Abp.AspNetCore.Mcp.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Volo.Abp;

namespace Microsoft.Extensions.DependencyInjection;

public static class AbpMcpAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Makes the MCP endpoint discoverable by an MCP client that has no token yet: publishes
    /// <c>/.well-known/oauth-protected-resource</c> (RFC 9728) and answers an unauthenticated MCP request
    /// with <c>401</c> plus <c>WWW-Authenticate: Bearer resource_metadata="…"</c> pointing at it.
    /// <para>
    /// Authentication itself is untouched - tokens are still validated by whatever bearer scheme the host
    /// already runs, through the default policy (see <see cref="AbpMcpAuthorizationMiddlewareResultHandler"/>
    /// for why that matters). The host must call <c>UseAuthentication()</c>, which is what serves the
    /// metadata document.
    /// </para>
    /// <para>
    /// Without this, an unauthenticated MCP request gets whatever the host's default challenge is - in an
    /// ABP MVC host, a <c>302</c> to the login page, which no MCP client can act on.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureResourceMetadata">
    /// Describes this resource server. Set at least <see cref="ProtectedResourceMetadata.Resource"/> (this
    /// host's public origin) and <see cref="ProtectedResourceMetadata.AuthorizationServers"/>. Both must be
    /// configured values, never derived from the incoming request's Host header.
    /// </param>
    /// <param name="configureOptions">
    /// Optional further settings of the MCP authentication scheme - for example
    /// <see cref="McpAuthenticationOptions.ResourceMetadataUri"/> behind a reverse proxy, where the SDK's
    /// default (built from the request's own scheme and host) would not be the public address.
    /// </param>
    public static IServiceCollection AddAbpMcpAuthenticationDiscovery(
        this IServiceCollection services,
        Action<ProtectedResourceMetadata> configureResourceMetadata,
        Action<McpAuthenticationOptions>? configureOptions = null)
    {
        Check.NotNull(services, nameof(services));
        Check.NotNull(configureResourceMetadata, nameof(configureResourceMetadata));

        // Built and checked now rather than when the scheme's options are first resolved, so a host that
        // forgot a value fails at startup - not with a 500 on the first anonymous GET of the metadata
        // document, which is what the SDK does when ResourceMetadata is missing.
        var metadata = new ProtectedResourceMetadata();
        configureResourceMetadata(metadata);
        if (metadata.Resource.IsNullOrWhiteSpace() || metadata.AuthorizationServers.Count == 0)
        {
            throw new AbpException(
                "AddAbpMcpAuthenticationDiscovery needs ProtectedResourceMetadata.Resource and at least one " +
                "AuthorizationServers entry: an MCP client cannot discover where to authenticate without them.");
        }

        // The scheme, the login-page fix and the challenge handler are registered once; every call (re)sets
        // the metadata, the last one winning like any other options - so a second call reconfigures rather
        // than failing on a scheme registered twice.
        if (!services.IsAdded<AbpMcpAuthenticationDiscoveryMarker>())
        {
            services.AddSingleton<AbpMcpAuthenticationDiscoveryMarker>();
            services.AddAuthentication().AddMcp();

            // AddMcp() gives the scheme a DisplayName, which ABP's Account module reads as reason enough to
            // render it as an external-login button on /Account/Login. It is not a login provider - it exists
            // only for the metadata document and the endpoint's 401 - so the name is cleared. Discovery and
            // the challenge both key off the scheme name, not its DisplayName.
            services.Configure<AuthenticationOptions>(options =>
            {
                var mcpScheme = options.Schemes.FirstOrDefault(scheme => scheme.Name == McpAuthenticationDefaults.AuthenticationScheme);
                if (mcpScheme != null)
                {
                    mcpScheme.DisplayName = null;
                }
            });

            DecorateAuthorizationMiddlewareResultHandler(services);
        }

        services.Configure<McpAuthenticationOptions>(McpAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ResourceMetadata = metadata;

            // The SDK forwards authentication to a scheme literally named "Bearer" by default - the one its
            // JwtBearer samples register, and one an ABP host usually does not have, so anything that ever
            // authenticated through this scheme would fail with "no handler for Bearer". This scheme never
            // authenticates here (tokens go through the host's own bearer scheme on the default policy), and
            // without the forward its handler just answers "no result". A host can still set one below.
            options.ForwardAuthenticate = null;

            configureOptions?.Invoke(options);
        });

        return services;
    }

    /// <summary>
    /// Wraps whichever <see cref="IAuthorizationMiddlewareResultHandler"/> is registered now, keeping its
    /// lifetime - ASP.NET Core resolves the handler from the request's services on every request, so a
    /// scoped or transient handler stays scoped or transient instead of being captured once. One registered
    /// later still replaces this wrapper; <see cref="AbpMcpAuthenticationDiscoveryChecker"/> fails startup
    /// when that happens rather than letting the MCP 401 silently turn back into a login redirect.
    /// </summary>
    private static void DecorateAuthorizationMiddlewareResultHandler(IServiceCollection services)
    {
        var existing = services.LastOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler) && !descriptor.IsKeyedService);

        services.Replace(ServiceDescriptor.Describe(
            typeof(IAuthorizationMiddlewareResultHandler),
            serviceProvider => new AbpMcpAuthorizationMiddlewareResultHandler(CreateInner(serviceProvider, existing)),
            existing?.Lifetime ?? ServiceLifetime.Singleton));
    }

    private static IAuthorizationMiddlewareResultHandler CreateInner(IServiceProvider serviceProvider, ServiceDescriptor? descriptor)
    {
        if (descriptor == null)
        {
            // Nothing registered yet: AddAuthorization() would add exactly this, with TryAdd - which our
            // registration now pre-empts.
            return new AuthorizationMiddlewareResultHandler();
        }

        if (descriptor.ImplementationInstance != null)
        {
            return (IAuthorizationMiddlewareResultHandler)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return (IAuthorizationMiddlewareResultHandler)descriptor.ImplementationFactory(serviceProvider);
        }

        return (IAuthorizationMiddlewareResultHandler)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
    }
}
