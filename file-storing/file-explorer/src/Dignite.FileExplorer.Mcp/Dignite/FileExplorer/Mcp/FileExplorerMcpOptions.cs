using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dignite.FileExplorer.Files;
using Volo.Abp;

namespace Dignite.FileExplorer.Mcp;

public class FileExplorerMcpOptions
{
    /// <summary>
    /// The blob containers an MCP client may use, each with a description written for a model - what the
    /// container is for, so it can pick the right one. Empty by default: nothing is exposed until the host
    /// lists it.
    /// </summary>
    /// <example><c>options.Containers.Add("site-images", "Images used in site content.");</c></example>
    public FileExplorerMcpContainerList Containers { get; } = new();

    /// <summary>
    /// The largest file, in bytes, <c>file_explorer_upload_file</c> accepts, whatever the container allows.
    /// Defaults to 5 MB. The bytes arrive base64-encoded inside a JSON-RPC message, so the request is
    /// buffered whole before any container rule can run; the MCP endpoint's request-body limit
    /// (<c>AbpMcpServerOptions.MaxRequestBodySize</c>) is raised, when it is lower, to just fit a file of this
    /// size (<see cref="FileExplorerMcpServerOptionsSetup"/>), so an oversized request is refused before it is
    /// read rather than after.
    /// </summary>
    public long MaxUploadSize { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// What <c>file_explorer_upload_file</c> actually accepts for a container: the container's own limit
    /// where it has one, never more than <see cref="MaxUploadSize"/>. The one rule both the upload and the
    /// container listing use, so what is advertised is what is enforced.
    /// </summary>
    public virtual long GetMaxUploadSize(FileContainerConfigurationDto configuration)
    {
        return configuration.MaxBlobSize > 0
            ? Math.Min(configuration.MaxBlobSize, MaxUploadSize)
            : MaxUploadSize;
    }
}

public class FileExplorerMcpContainer
{
    public FileExplorerMcpContainer(string name, string description)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        Description = Check.NotNullOrWhiteSpace(description, nameof(description));
    }

    /// <summary>The blob container's registered name.</summary>
    public string Name { get; }

    /// <summary>What the container is for, written for a model.</summary>
    public string Description { get; }
}

/// <summary>
/// The exposed containers. A <see cref="Collection{T}"/> rather than a <see cref="List{T}"/> so that every way
/// in - <c>Add</c>, <c>Insert</c>, the indexer - goes through the same duplicate check.
/// </summary>
public class FileExplorerMcpContainerList : Collection<FileExplorerMcpContainer>
{
    public FileExplorerMcpContainerList Add(string name, string description)
    {
        Add(new FileExplorerMcpContainer(name, description));
        return this;
    }

    protected override void InsertItem(int index, FileExplorerMcpContainer item)
    {
        EnsureNotExposedYet(item, ignoreIndex: -1);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, FileExplorerMcpContainer item)
    {
        EnsureNotExposedYet(item, ignoreIndex: index);
        base.SetItem(index, item);
    }

    /// <summary>Case-insensitive, the same as File Explorer's own container-name comparisons.</summary>
    public FileExplorerMcpContainer? Find(string name)
    {
        return this.FirstOrDefault(container => string.Equals(container.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureNotExposedYet(FileExplorerMcpContainer item, int ignoreIndex)
    {
        Check.NotNull(item, nameof(item));
        for (var i = 0; i < Count; i++)
        {
            if (i != ignoreIndex && string.Equals(this[i].Name, item.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new AbpException($"The blob container '{item.Name}' is already exposed to MCP.");
            }
        }
    }
}
