using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement;

/// <summary>How many times the grant store was asked, in one test application.</summary>
public class ResourcePermissionCheckCounter : ISingletonDependency
{
    private int _count;

    public int Count => _count;

    public void Add()
    {
        Interlocked.Increment(ref _count);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
    }
}

/// <summary>
/// ABP's resource-permission checker, counted: the tests show that a listing asks it once, for the container, never
/// for each directory.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IResourcePermissionChecker))]
public class CountingResourcePermissionChecker : IResourcePermissionChecker, ITransientDependency
{
    private readonly ResourcePermissionChecker _inner;
    private readonly ResourcePermissionCheckCounter _counter;

    public CountingResourcePermissionChecker(ResourcePermissionChecker inner, ResourcePermissionCheckCounter counter)
    {
        _inner = inner;
        _counter = counter;
    }

    public Task<bool> IsGrantedAsync(string name, string resourceName, string resourceKey)
    {
        _counter.Add();
        return _inner.IsGrantedAsync(name, resourceName, resourceKey);
    }

    public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name, string resourceName, string resourceKey)
    {
        _counter.Add();
        return _inner.IsGrantedAsync(claimsPrincipal, name, resourceName, resourceKey);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names, string resourceName, string resourceKey)
    {
        _counter.Add();
        return _inner.IsGrantedAsync(names, resourceName, resourceKey);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(
        ClaimsPrincipal? claimsPrincipal,
        string[] names,
        string resourceName,
        string resourceKey)
    {
        _counter.Add();
        return _inner.IsGrantedAsync(claimsPrincipal, names, resourceName, resourceKey);
    }
}
