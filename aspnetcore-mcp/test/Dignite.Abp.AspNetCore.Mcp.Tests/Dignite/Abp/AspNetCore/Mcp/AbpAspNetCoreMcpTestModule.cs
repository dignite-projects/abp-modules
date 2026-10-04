using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Abp.AspNetCore.Mcp;

[DependsOn(
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAspNetCoreMcpModule),
    typeof(AbpAutofacModule)
)]
public class AbpAspNetCoreMcpTestModule : AbpModule
{
    public const string ServerName = "Test MCP server";
    public const string HostInstructions = "Host section.";
    public const string ModuleInstructions = "Test module section.";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddAuthentication(options => options.DefaultScheme = TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, null);

        context.Services.AddAbpMcpAuthenticationDiscovery(metadata =>
        {
            metadata.Resource = "http://localhost";
            metadata.AuthorizationServers.Add("http://localhost");
        });

        Configure<AbpPermissionOptions>(options => options.ValueProviders.Add<TestClaimPermissionValueProvider>());

        Configure<AbpMcpServerOptions>(options =>
        {
            options.ServerName = ServerName;
            options.ServerVersion = "9.9.9";
            options.Instructions = HostInstructions;
        });

        context.Services.AddAbpMcpModule("test", mcp => mcp
            .AddTools<TestTools>()
            .AddResources<TestResources>()
            .AddResourceListContributor<TestResourceListContributor>()
            .AddInstructions(ModuleInstructions));
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseAuthentication();

        // Stands in for ABP's UseDynamicClaims(): after authentication, refresh the principal with claims
        // the token itself does not carry. If the MCP endpoint re-authenticated through a named scheme,
        // these would be lost again before the tools are filtered.
        app.Use(async (httpContext, next) =>
        {
            var dynamicPermission = httpContext.Request.Headers[TestAuthenticationHandler.DynamicPermissionHeader].ToString();
            if (!dynamicPermission.IsNullOrEmpty() && httpContext.User.Identity?.IsAuthenticated == true)
            {
                var identity = new ClaimsIdentity(httpContext.User.Claims, httpContext.User.Identity.AuthenticationType);
                identity.AddClaim(new Claim(TestClaimPermissionValueProvider.ClaimType, dynamicPermission));
                httpContext.User = new ClaimsPrincipal(identity);
            }

            await next(httpContext);
        });

        app.UseAuthorization();
        app.UseConfiguredEndpoints();
    }
}
