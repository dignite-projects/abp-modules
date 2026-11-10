using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Drive;

/// <summary>
/// ABP's in-memory provider as one store for the whole test application. The original is transient and keeps its
/// BLOBs per instance, so two <c>IBlobContainerFactory</c> instances - the manager's and a test's - would see
/// different storage, and an assertion that a BLOB is gone would pass whatever happened.
/// </summary>
[Dependency(ServiceLifetime.Singleton, ReplaceServices = true)]
[ExposeServices(typeof(MemoryBlobProvider), typeof(IBlobProvider))]
public class SingletonMemoryBlobProvider : MemoryBlobProvider
{
    public SingletonMemoryBlobProvider(ICurrentTenant currentTenant)
        : base(currentTenant)
    {
    }
}
