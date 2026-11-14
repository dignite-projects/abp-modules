using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// ABP's resource permission dialog on a container, through the application service it calls
/// (<see cref="IPermissionAppService"/>): it saves every permission of the resource for whoever it edits, so a
/// definition it cannot save for a user or a role would fail the dialog.
/// </summary>
public abstract class BlobManagementPermissionDialog_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Container = BlobManagementTestContainers.Documents;

    private static readonly Guid TenantId = Guid.Parse("4b0f2a43-9a52-4c3e-8a0b-0c1d2e3f4a5b");

    private readonly IPermissionAppService _permissions;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();

    protected BlobManagementPermissionDialog_Tests()
    {
        _permissions = GetRequiredService<IPermissionAppService>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Theory]
    [InlineData(false, UserResourcePermissionValueProvider.ProviderName)]
    [InlineData(false, RoleResourcePermissionValueProvider.ProviderName)]
    [InlineData(true, UserResourcePermissionValueProvider.ProviderName)]
    [InlineData(true, RoleResourcePermissionValueProvider.ProviderName)]
    public async Task The_Dialog_Should_Save_A_Containers_Permissions_For_A_User_Or_A_Role(bool inTenant, string providerName)
    {
        using (_currentTenant.Change(inTenant ? TenantId : null))
        {
            await _permissionManager.GrantAsync(_admin, BlobManagementPermissions.ManagePermissions);
            var providerKey = providerName == UserResourcePermissionValueProvider.ProviderName ? _alice.ToString() : "editors";

            using (_principalAccessor.As(_admin))
            {
                await _permissions.UpdateResourceAsync(BlobManagementResourceNames.Container, Container, new UpdateResourcePermissionsDto
                {
                    ProviderName = providerName,
                    ProviderKey = providerKey,
                    Permissions = new() { BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.CreateDirectories }
                });

                var saved = await _permissions.GetResourceByProviderAsync(
                    BlobManagementResourceNames.Container, Container, providerName, providerKey);
                saved.Permissions.Where(p => p.IsGranted).Select(p => p.Name).OrderBy(n => n).ShouldBe(new[]
                {
                    BlobManagementPermissions.Container.CreateDirectories,
                    BlobManagementPermissions.Container.View
                });
            }
        }
    }

    [Fact]
    public async Task The_Files_Permission_Should_Be_Offered_On_The_Host_Side_Only()
    {
        await _permissionManager.GrantAsync(_admin, BlobManagementPermissions.ManagePermissions);
        using (_principalAccessor.As(_admin))
        {
            (await _permissions.GetResourceDefinitionsAsync(BlobManagementResourceNames.Container))
                .Permissions.Select(p => p.Name).ShouldContain(BlobManagementPermissions.Container.Files);
        }

        using (_currentTenant.Change(TenantId))
        {
            await _permissionManager.GrantAsync(_admin, BlobManagementPermissions.ManagePermissions);
            using (_principalAccessor.As(_admin))
            {
                var names = (await _permissions.GetResourceDefinitionsAsync(BlobManagementResourceNames.Container))
                    .Permissions.Select(p => p.Name).ToList();
                names.ShouldNotContain(BlobManagementPermissions.Container.Files);
                names.ShouldContain(BlobManagementPermissions.Container.View);
            }
        }
    }
}
