using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Authenticates a request carrying <see cref="UserHeader"/>, granting the permissions listed in
/// <see cref="PermissionsHeader"/> as claims - the stand-in for a bearer token.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string PermissionsHeader = "X-Test-Permissions";
    public const string DynamicPermissionHeader = "X-Test-Dynamic-Permission";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        claims.AddRange(Request.Headers[PermissionsHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(permission => new Claim(TestClaimPermissionValueProvider.ClaimType, permission)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

/// <summary>Grants exactly the permissions the principal carries as <see cref="ClaimType"/> claims.</summary>
public class TestClaimPermissionValueProvider : PermissionValueProvider
{
    public const string ClaimType = "test_permission";

    public TestClaimPermissionValueProvider(IPermissionStore permissionStore)
        : base(permissionStore)
    {
    }

    public override string Name => "TestClaim";

    public override Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
    {
        return Task.FromResult(IsGranted(context.Principal, context.Permission.Name)
            ? PermissionGrantResult.Granted
            : PermissionGrantResult.Undefined);
    }

    public override Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var result = new MultiplePermissionGrantResult();
        foreach (var permission in context.Permissions)
        {
            result.Result[permission.Name] = IsGranted(context.Principal, permission.Name)
                ? PermissionGrantResult.Granted
                : PermissionGrantResult.Undefined;
        }

        return Task.FromResult(result);
    }

    private static bool IsGranted(ClaimsPrincipal? principal, string permissionName)
    {
        return principal?.FindAll(ClaimType).Any(claim => claim.Value == permissionName) == true;
    }
}

public class TestPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public const string Secret = "Test.Secret";

    public override void Define(IPermissionDefinitionContext context)
    {
        context.AddGroup("Test").AddPermission(Secret);
    }
}
