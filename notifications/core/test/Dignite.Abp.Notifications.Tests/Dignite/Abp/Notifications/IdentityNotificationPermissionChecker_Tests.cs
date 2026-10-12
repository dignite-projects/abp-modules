using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The Identity permission checker builds the principal that <see cref="IPermissionChecker"/> reads (user id, every
/// role, the tenant) from <see cref="IUserRoleFinder"/> alone: no Identity repository, no claims principal factory.
/// Both ABP services are substituted; the real <see cref="ICurrentTenant"/> supplies the tenant context.
/// </summary>
public class IdentityNotificationPermissionChecker_Tests : AbpIntegratedTest<IdentityPermissionCheckerTestModule>
{
    private const string Permission = "Test.Permission";

    private readonly INotificationPermissionChecker _checker;
    private readonly IUserRoleFinder _roleFinder;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ICurrentTenant _currentTenant;

    public IdentityNotificationPermissionChecker_Tests()
    {
        _checker = GetRequiredService<INotificationPermissionChecker>();
        _roleFinder = GetRequiredService<IUserRoleFinder>();
        _permissionChecker = GetRequiredService<IPermissionChecker>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public void Identity_module_provides_the_notification_permission_checker()
    {
        _checker.ShouldBeOfType<IdentityNotificationPermissionChecker>();
    }

    [Fact]
    public async Task Principal_carries_the_user_id_and_every_role()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin", "editor", "viewer" });
        var principal = CapturePrincipal();

        await _checker.IsGrantedAsync(userId, Permission);

        principal().FindFirst(AbpClaimTypes.UserId)!.Value.ShouldBe(userId.ToString());
        principal().FindAll(AbpClaimTypes.Role).Select(c => c.Value)
            .ShouldBe(new[] { "admin", "editor", "viewer" }, ignoreOrder: true);
    }

    [Fact]
    public async Task Principal_is_authenticated_and_exposes_the_claims_through_the_abp_helpers()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });
        var principal = CapturePrincipal();

        using (_currentTenant.Change(tenantId))
        {
            await _checker.IsGrantedAsync(userId, Permission);
        }

        principal().Identity!.IsAuthenticated.ShouldBeTrue();
        principal().FindUserId().ShouldBe(userId);
        principal().FindTenantId().ShouldBe(tenantId);
    }

    [Fact]
    public async Task Host_principal_has_no_tenant_claim()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });
        var principal = CapturePrincipal();

        await _checker.IsGrantedAsync(userId, Permission);

        principal().FindAll(AbpClaimTypes.TenantId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Tenant_principal_carries_the_ambient_tenant()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });
        var principal = CapturePrincipal();

        using (_currentTenant.Change(tenantId))
        {
            await _checker.IsGrantedAsync(userId, Permission);
        }

        principal().FindAll(AbpClaimTypes.TenantId).Select(c => c.Value).ShouldBe(new[] { tenantId.ToString() });
    }

    [Fact]
    public async Task Tenant_claim_follows_the_ambient_tenant_on_every_call()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(Array.Empty<string>());
        var principal = CapturePrincipal();

        using (_currentTenant.Change(tenantId))
        {
            await _checker.IsGrantedAsync(userId, Permission);
            principal().FindTenantId().ShouldBe(tenantId);
        }

        // Back on the host: the next check does not inherit the previous tenant.
        await _checker.IsGrantedAsync(userId, Permission);
        principal().FindTenantId().ShouldBeNull();
    }

    [Fact]
    public async Task User_without_roles_is_still_checked_with_only_the_user_id()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(Array.Empty<string>());
        var principal = CapturePrincipal();

        await _checker.IsGrantedAsync(userId, Permission);

        principal().FindFirst(AbpClaimTypes.UserId)!.Value.ShouldBe(userId.ToString());
        principal().FindAll(AbpClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task Asks_for_exactly_the_permission_and_user_it_was_given()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });

        await _checker.IsGrantedAsync(userId, Permission);

        await _roleFinder.Received(1).GetRoleNamesAsync(userId);
        await _permissionChecker.Received(1).IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Permission);
    }

    [Fact]
    public async Task Passes_a_granted_permission_through()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });
        _permissionChecker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Permission).Returns(true);

        (await _checker.IsGrantedAsync(userId, Permission)).ShouldBeTrue();
    }

    [Fact]
    public async Task Passes_a_denied_permission_through()
    {
        var userId = Guid.NewGuid();
        _roleFinder.GetRoleNamesAsync(userId).Returns(new[] { "admin" });
        _permissionChecker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Permission).Returns(false);

        (await _checker.IsGrantedAsync(userId, Permission)).ShouldBeFalse();
    }

    /// <summary>Records the principal handed to <see cref="IPermissionChecker"/>; read it after the call.</summary>
    private Func<ClaimsPrincipal> CapturePrincipal()
    {
        ClaimsPrincipal? captured = null;
        _permissionChecker.IsGrantedAsync(Arg.Do<ClaimsPrincipal>(p => captured = p), Arg.Any<string>())
            .Returns(true);
        return () => captured ?? throw new InvalidOperationException("The permission checker was not called.");
    }
}

[DependsOn(
    typeof(AbpNotificationsIdentityModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class IdentityPermissionCheckerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The two seams the checker talks to. No Identity repository, claims principal factory or database exists.
        context.Services.AddSingleton(Substitute.For<IUserRoleFinder>());
        context.Services.AddSingleton(Substitute.For<IPermissionChecker>());
    }
}

/// <summary>
/// The monolith composition: a process that carries Identity.Domain already has an <see cref="IUserRoleFinder"/>
/// (<c>UserRoleFinder</c> over the Identity repository), so the checker needs nothing else from the host.
/// </summary>
public class IdentityNotificationPermissionCheckerInMonolith_Tests
    : AbpIntegratedTest<IdentityMonolithPermissionCheckerTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Identity_domain_supplies_the_role_finder_the_checker_reads_roles_through()
    {
        var userId = Guid.NewGuid();
        GetRequiredService<IIdentityUserRepository>().GetRoleNamesAsync(userId)
            .Returns(new List<string> { "admin", "editor" });
        ClaimsPrincipal? principal = null;
        GetRequiredService<IPermissionChecker>()
            .IsGrantedAsync(Arg.Do<ClaimsPrincipal>(p => principal = p), Arg.Any<string>())
            .Returns(true);

        GetRequiredService<IUserRoleFinder>().ShouldBeOfType<UserRoleFinder>();
        (await GetRequiredService<INotificationPermissionChecker>().IsGrantedAsync(userId, "Test.Permission"))
            .ShouldBeTrue();

        principal.ShouldNotBeNull();
        principal!.FindAll(AbpClaimTypes.Role).Select(c => c.Value)
            .ShouldBe(new[] { "admin", "editor" }, ignoreOrder: true);
    }
}

[DependsOn(
    typeof(AbpNotificationsIdentityModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class IdentityMonolithPermissionCheckerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Stand in for the EF Core / MongoDB Identity repositories a real monolith maps.
        context.Services.AddSingleton(Substitute.For<IIdentityUserRepository>());
        context.Services.AddSingleton(Substitute.For<IIdentityRoleRepository>());
        context.Services.AddSingleton(Substitute.For<IPermissionChecker>());
    }
}
