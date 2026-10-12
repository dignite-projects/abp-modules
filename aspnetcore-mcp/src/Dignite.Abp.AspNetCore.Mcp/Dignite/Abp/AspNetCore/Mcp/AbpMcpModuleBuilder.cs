using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// What a module uses, inside <see cref="AbpMcpServiceCollectionExtensions.AddAbpMcpModule"/>, to
/// contribute tools, resources, prompts and instructions to the application's MCP server.
/// <para>
/// <b>Prefer these over the SDK's own <c>WithTools&lt;T&gt;()</c> family, for two reasons.</b> They record
/// which module owns each primitive, which is what lets <see cref="AbpMcpPrimitiveValidator"/> enforce
/// the module's namespace and name both sides of a collision. And they resolve the tool class from ABP's
/// container on every call, where the SDK instead constructs it with <c>ActivatorUtilities</c> - so with
/// the SDK's registration a subclass registered with <c>[Dependency(ReplaceServices = true)]</c> is never
/// used, and overriding a virtual tool method silently does nothing.
/// </para>
/// </summary>
public class AbpMcpModuleBuilder
{
    private const BindingFlags PrimitiveMethodFlags =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly IServiceCollection _services;

    internal AbpMcpModuleBuilder(AbpMcpModule module, IServiceCollection services)
    {
        Module = module;
        _services = services;
    }

    public AbpMcpModule Module { get; }

    /// <summary>
    /// Overrides the URI scheme this module's resources must use; see <see cref="AbpMcpModule.UriScheme"/>.
    /// </summary>
    public virtual AbpMcpModuleBuilder UseUriScheme(string uriScheme)
    {
        Module.UriScheme = uriScheme;
        return this;
    }

    /// <summary>
    /// Adds a section to the instructions the server hands a client on connect. Describe only this
    /// module's own tools - another module may or may not be installed alongside it.
    /// </summary>
    public virtual AbpMcpModuleBuilder AddInstructions(string instructions)
    {
        Check.NotNullOrWhiteSpace(instructions, nameof(instructions));
        Module.AddInstructions(instructions);
        return this;
    }

    /// <summary>
    /// Adds a contributor of dynamic entries to <c>resources/list</c> - see
    /// <see cref="IAbpMcpResourceListContributor"/>. Use it instead of the SDK's
    /// <c>WithListResourcesHandler</c>, a single slot that the next module to set it would overwrite.
    /// </summary>
    public virtual AbpMcpModuleBuilder AddResourceListContributor<TContributor>()
        where TContributor : class, IAbpMcpResourceListContributor
    {
        _services.TryAddTransient<TContributor>();
        Module.AddResourceListContributor(typeof(TContributor));
        return this;
    }

    /// <summary>Registers every <c>[McpServerTool]</c> method of <typeparamref name="TToolType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddTools<TToolType>()
        where TToolType : class
    {
        return AddTools(typeof(TToolType));
    }

    /// <summary>Registers every <c>[McpServerTool]</c> method of <paramref name="toolType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddTools(Type toolType)
    {
        foreach (var method in FindPrimitiveMethods<McpServerToolAttribute>(toolType))
        {
            var registration = Register(AbpMcpPrimitiveKind.Tool, toolType, method);
            _services.AddSingleton<McpServerTool>(serviceProvider => serviceProvider
                .GetRequiredService<AbpMcpPrimitiveCache>()
                .GetOrCreate(registration, () => method.IsStatic
                    ? McpServerTool.Create(method, (object?)null, new McpServerToolCreateOptions { Services = serviceProvider })
                    : McpServerTool.Create(method, request => ResolveTarget(request, toolType), new McpServerToolCreateOptions { Services = serviceProvider })));
        }

        return this;
    }

    /// <summary>Registers every <c>[McpServerResource]</c> method of <typeparamref name="TResourceType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddResources<TResourceType>()
        where TResourceType : class
    {
        return AddResources(typeof(TResourceType));
    }

    /// <summary>Registers every <c>[McpServerResource]</c> method of <paramref name="resourceType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddResources(Type resourceType)
    {
        foreach (var method in FindPrimitiveMethods<McpServerResourceAttribute>(resourceType))
        {
            var registration = Register(AbpMcpPrimitiveKind.Resource, resourceType, method);
            _services.AddSingleton<McpServerResource>(serviceProvider => serviceProvider
                .GetRequiredService<AbpMcpPrimitiveCache>()
                .GetOrCreate(registration, () => method.IsStatic
                    ? McpServerResource.Create(method, (object?)null, new McpServerResourceCreateOptions { Services = serviceProvider })
                    : McpServerResource.Create(method, request => ResolveTarget(request, resourceType), new McpServerResourceCreateOptions { Services = serviceProvider })));
        }

        return this;
    }

    /// <summary>Registers every <c>[McpServerPrompt]</c> method of <typeparamref name="TPromptType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddPrompts<TPromptType>()
        where TPromptType : class
    {
        return AddPrompts(typeof(TPromptType));
    }

    /// <summary>Registers every <c>[McpServerPrompt]</c> method of <paramref name="promptType"/>.</summary>
    public virtual AbpMcpModuleBuilder AddPrompts(Type promptType)
    {
        foreach (var method in FindPrimitiveMethods<McpServerPromptAttribute>(promptType))
        {
            var registration = Register(AbpMcpPrimitiveKind.Prompt, promptType, method);
            _services.AddSingleton<McpServerPrompt>(serviceProvider => serviceProvider
                .GetRequiredService<AbpMcpPrimitiveCache>()
                .GetOrCreate(registration, () => method.IsStatic
                    ? McpServerPrompt.Create(method, (object?)null, new McpServerPromptCreateOptions { Services = serviceProvider })
                    : McpServerPrompt.Create(method, request => ResolveTarget(request, promptType), new McpServerPromptCreateOptions { Services = serviceProvider })));
        }

        return this;
    }

    protected virtual AbpMcpPrimitiveRegistration Register(AbpMcpPrimitiveKind kind, Type declaringType, MethodInfo method)
    {
        if (!method.IsStatic)
        {
            // Usually a no-op: a tool class is normally an ITransientDependency and ABP has registered it
            // already. This only covers one that is not, so resolving it per call cannot fail.
            _services.TryAddTransient(declaringType);
        }

        return Module.AddPrimitive(kind, declaringType, method);
    }

    /// <summary>
    /// The same method discovery as the SDK's <c>WithTools&lt;T&gt;()</c>, but a type with nothing to
    /// register is an error rather than a silent no-op - it is always a mistake (the wrong type, or an
    /// attribute that was forgotten).
    /// </summary>
    protected virtual IReadOnlyList<MethodInfo> FindPrimitiveMethods<TAttribute>(Type type)
        where TAttribute : Attribute
    {
        Check.NotNull(type, nameof(type));

        var methods = type.GetMethods(PrimitiveMethodFlags)
            .Where(method => method.GetCustomAttribute<TAttribute>() != null)
            .ToList();

        if (methods.Count == 0)
        {
            throw new AbpException(
                $"{type.FullName} has no method marked with [{typeof(TAttribute).Name.RemovePostFix("Attribute")}], " +
                $"so registering it with MCP module '{Module.Name}' would add nothing.");
        }

        return methods;
    }

    /// <summary>
    /// Resolves the tool class from the request's scope - the per-request scope in stateless mode - so
    /// ABP's registration of it (replacements, interceptors, scoped dependencies) is what actually runs.
    /// </summary>
    protected static object ResolveTarget(MessageContext context, Type type)
    {
        if (context.Services == null)
        {
            throw new AbpException(
                $"Cannot resolve {type.FullName} for an MCP request: the request carries no service provider.");
        }

        return context.Services.GetRequiredService(type);
    }
}
