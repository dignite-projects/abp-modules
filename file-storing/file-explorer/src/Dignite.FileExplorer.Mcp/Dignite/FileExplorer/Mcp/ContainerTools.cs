using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.FileExplorer.Mcp;

/// <summary>The entry point: which containers exist here, and what each accepts.</summary>
[McpServerToolType]
public class ContainerTools : ITransientDependency
{
    public ILogger<ContainerTools> Logger { get; set; } = NullLogger<ContainerTools>.Instance;

    protected IFileDescriptorAppService FileAppService { get; }

    protected FileExplorerMcpOptions Options { get; }

    public ContainerTools(IFileDescriptorAppService fileAppService, IOptions<FileExplorerMcpOptions> options)
    {
        FileAppService = fileAppService;
        Options = options.Value;
    }

    [McpServerTool(Name = "file_explorer_list_containers", Title = "List file containers", ReadOnly = true)]
    [Description(
        "Lists the file containers you may use, with each one's purpose, the largest file you may upload to " +
        "it, its allowed file types and its cells. Call this before any other file_explorer tool: they all " +
        "take a containerName from this list.")]
    public virtual async Task<List<FileExplorerMcpContainerDto>> ListContainersAsync()
    {
        var result = new List<FileExplorerMcpContainerDto>();
        foreach (var container in Options.Containers)
        {
            FileContainerConfigurationDto configuration;
            try
            {
                configuration = await FileAppService.GetFileContainerConfigurationAsync(container.Name);
            }
            catch (BusinessException exception)
            {
                // A listed name that is not a registered blob container (a typo, a container since removed)
                // is left out, and logged for the host to fix - rather than failing the tool every other
                // file_explorer tool tells the model to call first, for the containers that do work.
                Logger.LogWarning(exception,
                    "The blob container '{ContainerName}' is exposed to MCP but could not be read, so it is left out of file_explorer_list_containers.",
                    container.Name);
                continue;
            }

            result.Add(new FileExplorerMcpContainerDto
            {
                Name = container.Name,
                Description = container.Description,
                MaxUploadSize = Options.GetMaxUploadSize(configuration),
                AllowedFileTypes = configuration.AllowedFileTypeNames?.ToList() ?? new List<string>(),
                Cells = configuration.FileCells?
                    .Select(cell => new FileExplorerMcpCellDto { Name = cell.Name, DisplayName = cell.DisplayName })
                    .ToList() ?? new List<FileExplorerMcpCellDto>()
            });
        }

        return result;
    }
}

public class FileExplorerMcpContainerDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The largest file, in bytes, file_explorer_upload_file accepts for this container.</summary>
    public long MaxUploadSize { get; set; }

    /// <summary>Allowed file extensions; empty means the container does not restrict them.</summary>
    public List<string> AllowedFileTypes { get; set; } = new();

    /// <summary>The cells a file may be placed in (the cellName argument); empty if the container has none.</summary>
    public List<FileExplorerMcpCellDto> Cells { get; set; } = new();
}

public class FileExplorerMcpCellDto
{
    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
