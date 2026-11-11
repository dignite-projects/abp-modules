using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace Dignite.Drive;

// The user and role grant writers of ABP's PermissionManagement.Domain.Identity, without the Identity module the tests
// have no use for: they store grants under the same provider names ("U", "R") the value providers read.

public class TestUserPermissionManagementProvider : PermissionManagementProvider
{
    public TestUserPermissionManagementProvider(
        IPermissionGrantRepository permissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
        : base(permissionGrantRepository, guidGenerator, currentTenant)
    {
    }

    public override string Name => UserPermissionValueProvider.ProviderName;
}

public class TestRolePermissionManagementProvider : PermissionManagementProvider
{
    public TestRolePermissionManagementProvider(
        IPermissionGrantRepository permissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
        : base(permissionGrantRepository, guidGenerator, currentTenant)
    {
    }

    public override string Name => RolePermissionValueProvider.ProviderName;
}
