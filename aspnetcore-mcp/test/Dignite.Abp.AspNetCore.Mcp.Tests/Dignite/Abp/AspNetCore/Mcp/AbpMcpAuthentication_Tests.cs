using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// The discovery challenge: what it takes over, what it must leave to the handler it wraps, and how its
/// registration behaves alongside the host's own.
/// </summary>
public class AbpMcpAuthentication_Tests
{
    [Fact]
    public async Task Should_Answer_An_Unauthenticated_Mcp_Request_With_The_Mcp_Challenge()
    {
        var (handler, inner, authentication, context) = CreateHandler(isMcpEndpoint: true);

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Challenge());

        authentication.ChallengedScheme.ShouldBe(McpAuthenticationDefaults.AuthenticationScheme);
        inner.Called.ShouldBeFalse();
    }

    /// <summary>A 403 is an answer for an authenticated caller; it stays the host's to give.</summary>
    [Fact]
    public async Task Should_Leave_A_Forbidden_Mcp_Request_To_The_Wrapped_Handler()
    {
        var (handler, inner, authentication, context) = CreateHandler(isMcpEndpoint: true);

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Forbid());

        inner.Called.ShouldBeTrue();
        authentication.ChallengedScheme.ShouldBeNull();
    }

    /// <summary>Every other endpoint - the admin UI's pages included - keeps the host's own challenge.</summary>
    [Fact]
    public async Task Should_Leave_Other_Endpoints_To_The_Wrapped_Handler()
    {
        var (handler, inner, authentication, context) = CreateHandler(isMcpEndpoint: false);

        await handler.HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Challenge());

        inner.Called.ShouldBeTrue();
        authentication.ChallengedScheme.ShouldBeNull();
    }

    [Fact]
    public void Should_Reconfigure_Rather_Than_Fail_When_Called_Twice()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://first.example"));
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://second.example"));
        using var provider = services.BuildServiceProvider();

        // Materializing the authentication options would throw on a scheme registered twice.
        provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.Schemes
            .Count(scheme => scheme.Name == McpAuthenticationDefaults.AuthenticationScheme).ShouldBe(1);
        provider.GetRequiredService<IOptionsMonitor<McpAuthenticationOptions>>()
            .Get(McpAuthenticationDefaults.AuthenticationScheme).ResourceMetadata!.Resource.ShouldBe("https://second.example");
        services.Count(descriptor => descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler)).ShouldBe(1);
    }

    /// <summary>
    /// The SDK's default forwards authentication to a scheme named "Bearer", which ABP hosts do not have;
    /// anything authenticating through the MCP scheme would then fail. It must not forward unless told to.
    /// </summary>
    [Fact]
    public void Should_Not_Forward_Authentication_To_A_Bearer_Scheme_That_May_Not_Exist()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://example"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptionsMonitor<McpAuthenticationOptions>>()
            .Get(McpAuthenticationDefaults.AuthenticationScheme).ForwardAuthenticate.ShouldBeNull();
    }

    [Fact]
    public void Should_Let_The_Host_Choose_A_Forward_Target()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://example"), options => options.ForwardAuthenticate = "OpenIddict.Validation.AspNetCore");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptionsMonitor<McpAuthenticationOptions>>()
            .Get(McpAuthenticationDefaults.AuthenticationScheme).ForwardAuthenticate.ShouldBe("OpenIddict.Validation.AspNetCore");
    }

    /// <summary>
    /// ASP.NET Core resolves the handler per request; wrapping a scoped one in a singleton would capture
    /// whatever it was first built with.
    /// </summary>
    [Fact]
    public void Should_Keep_The_Wrapped_Handlers_Lifetime()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAuthorizationMiddlewareResultHandler, RecordingResultHandler>();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://example"));

        services.Single(descriptor => descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler))
            .Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthorizationMiddlewareResultHandler>()
            .ShouldBeOfType<AbpMcpAuthorizationMiddlewareResultHandler>();
    }

    [Fact]
    public void Should_Fail_Startup_When_The_Challenge_Handler_Is_Replaced_Afterwards()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://example"));
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, RecordingResultHandler>());
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<AbpException>(() =>
            AbpMcpAuthenticationDiscoveryChecker.EnsureChallengeHandlerIsInstalled(provider));
        exception.Message.ShouldContain(typeof(RecordingResultHandler).FullName!);
    }

    [Fact]
    public void Should_Pass_The_Startup_Check_When_The_Challenge_Handler_Is_In_Place()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpAuthenticationDiscovery(Metadata("https://example"));
        using var provider = services.BuildServiceProvider();

        Should.NotThrow(() => AbpMcpAuthenticationDiscoveryChecker.EnsureChallengeHandlerIsInstalled(provider));
    }

    private static readonly AuthorizationPolicy Policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    private static Action<ModelContextProtocol.Authentication.ProtectedResourceMetadata> Metadata(string resource)
    {
        return metadata =>
        {
            metadata.Resource = resource;
            metadata.AuthorizationServers.Add(resource);
        };
    }

    private static (AbpMcpAuthorizationMiddlewareResultHandler Handler, RecordingResultHandler Inner,
        RecordingAuthenticationService Authentication, HttpContext Context) CreateHandler(bool isMcpEndpoint)
    {
        var inner = new RecordingResultHandler();
        var authentication = new RecordingAuthenticationService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(authentication)
                .BuildServiceProvider()
        };
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            isMcpEndpoint ? new EndpointMetadataCollection(AbpMcpEndpointMetadata.Instance) : EndpointMetadataCollection.Empty,
            isMcpEndpoint ? "mcp" : "page"));

        return (new AbpMcpAuthorizationMiddlewareResultHandler(inner), inner, authentication, context);
    }

    public sealed class RecordingResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public bool Called { get; private set; }

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? ChallengedScheme { get; private set; }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ChallengedScheme = scheme;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
