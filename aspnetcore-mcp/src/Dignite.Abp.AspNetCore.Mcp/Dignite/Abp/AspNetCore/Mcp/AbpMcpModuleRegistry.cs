using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Every module that contributes to the MCP server, in registration order, with what it contributed.
/// <para>
/// Filled while services are being registered and read-only afterwards. It lives in the service
/// collection as a singleton instance so that each contributing module finds the same one regardless of
/// whether it registers before or after <see cref="AbpAspNetCoreMcpModule"/> runs.
/// </para>
/// </summary>
public class AbpMcpModuleRegistry
{
    private readonly List<AbpMcpModule> _modules = new();

    public IReadOnlyList<AbpMcpModule> Modules => _modules;

    public virtual AbpMcpModule? Find(string name)
    {
        return _modules.FirstOrDefault(module => module.Name == name);
    }

    internal AbpMcpModule GetOrAdd(string name)
    {
        var module = Find(name);
        if (module == null)
        {
            module = new AbpMcpModule(name);
            _modules.Add(module);
        }

        return module;
    }
}

internal static class AbpMcpModuleRegistryServiceCollectionExtensions
{
    public static AbpMcpModuleRegistry GetOrAddAbpMcpModuleRegistry(this IServiceCollection services)
    {
        var registry = services.GetSingletonInstanceOrNull<AbpMcpModuleRegistry>();
        if (registry == null)
        {
            registry = new AbpMcpModuleRegistry();
            services.AddSingleton(registry);
        }

        return registry;
    }
}
