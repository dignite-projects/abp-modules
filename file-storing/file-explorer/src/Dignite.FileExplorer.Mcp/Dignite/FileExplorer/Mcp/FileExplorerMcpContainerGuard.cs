using System;
using System.Linq;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.FileExplorer.Mcp;

/// <summary>
/// Keeps every tool inside the containers the host exposed in <see cref="FileExplorerMcpOptions"/>.
/// <para>
/// An addition to File Explorer's own per-container authorization, never a substitute: the application
/// services still decide whether the current user may act. This only decides whether MCP may ask.
/// </para>
/// </summary>
public class FileExplorerMcpContainerGuard : ITransientDependency
{
    protected FileExplorerMcpOptions Options { get; }

    public FileExplorerMcpContainerGuard(IOptions<FileExplorerMcpOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>
    /// The exposed container named <paramref name="containerName"/>; otherwise a not-found that lists the
    /// containers that are exposed, which is the answer the model needs.
    /// </summary>
    public virtual FileExplorerMcpContainer GetContainer(string containerName)
    {
        var container = Options.Containers.Find(containerName);
        if (container != null)
        {
            return container;
        }

        var exposed = Options.Containers.Count == 0
            ? "No container is exposed to MCP on this server."
            : "The containers you may use are: " + string.Join(", ", Options.Containers.Select(c => $"'{c.Name}'")) + ".";

        throw new FileExplorerMcpNotFoundException(
            $"There is no container named '{containerName}' available here. {exposed} " +
            "Call file_explorer_list_containers for their descriptions.");
    }

    /// <summary>
    /// Refuses a file or directory, found by id, that lives outside the exposed containers - reported
    /// exactly like a missing one (<see cref="NotFound"/>), so the tools do not confirm what exists elsewhere.
    /// </summary>
    public virtual void EnsureExposed(string containerName, string kind, Guid id)
    {
        if (Options.Containers.Find(containerName) == null)
        {
            throw NotFound(kind, id);
        }
    }

    /// <summary>
    /// The one answer for "nothing you may see has this id" - whether it does not exist, the caller may not
    /// read it, or it lives in a container not exposed to MCP. Distinct answers would let a caller probe ids
    /// and learn which exist.
    /// </summary>
    public virtual FileExplorerMcpNotFoundException NotFound(string kind, Guid id)
    {
        return new FileExplorerMcpNotFoundException($"There is no {kind} with id '{id}'.");
    }
}
