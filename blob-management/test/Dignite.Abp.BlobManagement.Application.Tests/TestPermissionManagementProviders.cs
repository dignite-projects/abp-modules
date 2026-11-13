using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace Dignite.Abp.BlobManagement;

// The user and role grant writers of ABP's PermissionManagement.Domain.Identity, and the client one of
// PermissionManagement.Domain.OpenIddict, without the Identity and OpenIddict modules the tests have no use for: they
// store grants under the same provider names ("U", "R", "C") the value providers read.

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

public class TestUserResourcePermissionManagementProvider : ResourcePermissionManagementProvider
{
    public TestUserResourcePermissionManagementProvider(
        IResourcePermissionGrantRepository resourcePermissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
        : base(resourcePermissionGrantRepository, guidGenerator, currentTenant)
    {
    }

    public override string Name => UserResourcePermissionValueProvider.ProviderName;
}

public class TestRoleResourcePermissionManagementProvider : ResourcePermissionManagementProvider
{
    public TestRoleResourcePermissionManagementProvider(
        IResourcePermissionGrantRepository resourcePermissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
        : base(resourcePermissionGrantRepository, guidGenerator, currentTenant)
    {
    }

    public override string Name => RoleResourcePermissionValueProvider.ProviderName;
}

/// <summary>
/// As ABP's <c>ApplicationResourcePermissionManagementProvider</c>: a client's grants are the deployment's, written and
/// read on the host side, which is where ABP's client value provider looks for them.
/// </summary>
public class TestClientResourcePermissionManagementProvider : ResourcePermissionManagementProvider
{
    public TestClientResourcePermissionManagementProvider(
        IResourcePermissionGrantRepository resourcePermissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
        : base(resourcePermissionGrantRepository, guidGenerator, currentTenant)
    {
    }

    public override string Name => ClientResourcePermissionValueProvider.ProviderName;

    public override System.Threading.Tasks.Task SetAsync(string name, string resourceName, string resourceKey, string providerKey, bool isGranted)
    {
        using (CurrentTenant.Change(null))
        {
            return base.SetAsync(name, resourceName, resourceKey, providerKey, isGranted);
        }
    }
}
