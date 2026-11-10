using System.Linq;
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
[DisableConventionalRegistration]
public class SingletonMemoryBlobProvider : MemoryBlobProvider
{
    public SingletonMemoryBlobProvider(ICurrentTenant currentTenant)
        : base(currentTenant)
    {
    }

    /// <summary>
    /// Replaces the original's registrations (as itself and as one of the <see cref="IBlobProvider"/>s) with this one,
    /// leaving every other provider registered.
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(MemoryBlobProvider)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<SingletonMemoryBlobProvider>();
        services.AddSingleton<MemoryBlobProvider>(sp => sp.GetRequiredService<SingletonMemoryBlobProvider>());
        services.AddSingleton<IBlobProvider>(sp => sp.GetRequiredService<SingletonMemoryBlobProvider>());
    }
}
