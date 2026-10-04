using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Checks, once at startup, that every tool, resource and prompt on the server has a unique name and
/// sits inside the namespace of the module that registered it.
/// <para>
/// <b>Why it has to exist.</b> The SDK merges every registered primitive into one collection with
/// <c>TryAdd</c>: a second tool with an existing name - or a second resource with an existing URI
/// template - is dropped without an error or a log line, and which one survives depends on module load
/// order. Templated resources are worse: a read is matched against them one by one in registration order,
/// so two different templates that both match a URI shadow each other just as silently. Namespaces
/// (<see cref="AbpMcpModule"/>) prevent all of that across modules by construction; this turns any
/// breach into a startup failure that names both sides.
/// </para>
/// <para>
/// It validates the raw registrations, before the SDK merges them, so it also sees what a merge would
/// hide - including primitives registered through the SDK's own <c>WithTools&lt;T&gt;()</c>, which belong
/// to no module and are rejected for that reason, unless the host opts in with
/// <see cref="AbpMcpServerOptions.AllowUnownedPrimitives"/>.
/// </para>
/// </summary>
public class AbpMcpPrimitiveValidator : ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    protected AbpMcpModuleRegistry Registry { get; }

    protected AbpMcpPrimitiveCache Cache { get; }

    protected AbpMcpServerOptions Options { get; }

    public AbpMcpPrimitiveValidator(
        IServiceProvider serviceProvider,
        AbpMcpModuleRegistry registry,
        AbpMcpPrimitiveCache cache,
        IOptions<AbpMcpServerOptions> options)
    {
        ServiceProvider = serviceProvider;
        Registry = registry;
        Cache = cache;
        Options = options.Value;
    }

    /// <exception cref="AbpException">Thrown with every problem found, not just the first.</exception>
    public virtual void Validate()
    {
        var errors = new List<string>();

        ValidateModules(errors);

        // Resolving the primitives also builds them - JSON schema generation included - so a malformed
        // tool signature fails here too, at startup, instead of on its first tools/list.
        ValidatePrimitives(
            ServiceProvider.GetServices<McpServerTool>().ToList(),
            "tool name",
            tool => tool.ProtocolTool.Name,
            (module, name) => name.StartsWith(module.NamePrefix, StringComparison.Ordinal),
            module => $"start with '{module.NamePrefix}'",
            errors);

        ValidatePrimitives(
            ServiceProvider.GetServices<McpServerPrompt>().ToList(),
            "prompt name",
            prompt => prompt.ProtocolPrompt.Name,
            (module, name) => name.StartsWith(module.NamePrefix, StringComparison.Ordinal),
            module => $"start with '{module.NamePrefix}'",
            errors);

        ValidatePrimitives(
            ServiceProvider.GetServices<McpServerResource>().ToList(),
            "resource URI template",
            resource => resource.ProtocolResourceTemplate.UriTemplate,
            (module, uriTemplate) => uriTemplate.StartsWith(module.UriScheme + ":", StringComparison.Ordinal),
            module => $"use the '{module.UriScheme}:' scheme",
            errors);

        if (errors.Count > 0)
        {
            throw new AbpException(
                "The MCP server's tools, resources and prompts are inconsistent:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => " - " + error)));
        }
    }

    protected virtual void ValidateModules(List<string> errors)
    {
        var modules = Registry.Modules;

        // "file" and "file_explorer" would both accept a tool named file_explorer_x, so the namespaces
        // must not nest - not just differ.
        foreach (var module in modules)
        {
            foreach (var other in modules.Where(other => other != module &&
                                                         other.Name.StartsWith(module.NamePrefix, StringComparison.Ordinal)))
            {
                errors.Add(
                    $"MCP module '{other.Name}' is nested inside the namespace of MCP module '{module.Name}' " +
                    $"(every name starting with '{other.NamePrefix}' also starts with '{module.NamePrefix}'). " +
                    "Module names must not be prefixes of one another.");
            }
        }

        foreach (var group in modules.GroupBy(module => module.UriScheme, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            errors.Add(
                $"MCP modules {string.Join(", ", group.Select(module => $"'{module.Name}'"))} all use the URI " +
                $"scheme '{group.Key}'. Each module needs a scheme of its own.");
        }
    }

    protected virtual void ValidatePrimitives<TPrimitive>(
        IReadOnlyList<TPrimitive> primitives,
        string identifierKind,
        Func<TPrimitive, string> getIdentifier,
        Func<AbpMcpModule, string, bool> isInsideNamespace,
        Func<AbpMcpModule, string> describeNamespace,
        List<string> errors)
        where TPrimitive : IMcpServerPrimitive
    {
        foreach (var primitive in primitives)
        {
            var identifier = getIdentifier(primitive);
            var owner = Cache.FindOwner(primitive);

            if (owner == null)
            {
                if (Options.AllowUnownedPrimitives)
                {
                    continue;
                }

                errors.Add(
                    $"The {identifierKind} '{identifier}' ({DescribeUnowned(primitive)}) was registered outside " +
                    "any MCP module. Register it through context.Services.AddAbpMcpModule(...) so it is owned " +
                    "by a namespace - or, for a third-party library that cannot be changed, set " +
                    "AbpMcpServerOptions.AllowUnownedPrimitives.");
            }
            else if (!isInsideNamespace(owner.Module, identifier))
            {
                errors.Add(
                    $"The {identifierKind} '{identifier}' from {owner} must {describeNamespace(owner.Module)}.");
            }
        }

        foreach (var group in primitives.GroupBy(getIdentifier, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            errors.Add(
                $"The {identifierKind} '{group.Key}' is registered {group.Count()} times: " +
                string.Join("; ", group.Select(primitive => Cache.FindOwner(primitive)?.ToString() ?? DescribeUnowned(primitive))) +
                ". The MCP SDK would keep one of them and silently drop the rest.");
        }
    }

    /// <summary>
    /// The best description available for a primitive registered outside a module: the SDK puts the
    /// source method first in the metadata of anything it built from a method.
    /// </summary>
    protected virtual string DescribeUnowned(IMcpServerPrimitive primitive)
    {
        var method = primitive.Metadata.OfType<MethodInfo>().FirstOrDefault();
        return method == null
            ? $"a {primitive.GetType().Name} of unknown origin"
            : $"{method.DeclaringType?.FullName}.{method.Name}";
    }
}
