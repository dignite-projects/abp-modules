using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;

namespace Dignite.Drive;

/// <summary>
/// Tenant feature values a test sets, in place of the Feature Management module's store. Tenants without a value get
/// the feature's default.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IFeatureStore), typeof(FakeFeatureStore))]
public class FakeFeatureStore : IFeatureStore, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, string> _tenantValues = new();

    public void SetForTenant(Guid tenantId, string name, string value)
    {
        _tenantValues[Key(tenantId.ToString(), name)] = value;
    }

    public Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        if (providerName != TenantFeatureValueProvider.ProviderName || providerKey == null)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(_tenantValues.TryGetValue(Key(providerKey, name), out var value) ? value : null);
    }

    private static string Key(string tenantId, string name)
    {
        return tenantId + "|" + name;
    }
}
