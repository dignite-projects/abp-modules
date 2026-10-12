using System;
using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;

namespace Microsoft.Extensions.DependencyInjection;

public static class AbpMcpServiceCollectionExtensions
{
    /// <summary>
    /// Contributes to the application's MCP server under the namespace <paramref name="name"/>: every
    /// tool and prompt registered here must be named <c>{name}_…</c>, and every resource URI must use the
    /// module's URI scheme (by default <paramref name="name"/> with <c>_</c> turned into <c>-</c>).
    /// <para>
    /// Call it from the contributing module's <c>ConfigureServices</c>, and have that module depend on
    /// <see cref="AbpAspNetCoreMcpModule"/>. Calling it again with the same name extends the same
    /// module - how a downstream package adds tools to a product's existing namespace.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// context.Services.AddAbpMcpModule("file_explorer", mcp => mcp
    ///     .AddTools&lt;FileExplorerTools&gt;()
    ///     .AddInstructions("..."));
    /// </code>
    /// </example>
    public static IServiceCollection AddAbpMcpModule(
        this IServiceCollection services,
        string name,
        Action<AbpMcpModuleBuilder> configure)
    {
        Check.NotNull(services, nameof(services));
        Check.NotNull(configure, nameof(configure));

        services.TryAddSingleton<AbpMcpPrimitiveCache>();

        var module = services.GetOrAddAbpMcpModuleRegistry().GetOrAdd(name);
        configure(new AbpMcpModuleBuilder(module, services));

        return services;
    }
}
