using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// Listing files and reading one. Each tool calls the file application service, which holds the authorization: a user
/// reads with the management permission. Nothing here stores, renames, moves or deletes a file - a file belongs to the
/// business system that keeps it, and that system's own tools change it.
/// </summary>
[McpServerToolType]
public class BlobManagementFileTools : ITransientDependency
{
    protected IBlobManagementFileAppService FileAppService { get; }

    public BlobManagementFileTools(IBlobManagementFileAppService fileAppService)
    {
        FileAppService = fileAppService;
    }

    [McpServerTool(Name = "blob_list_files", Title = "List files", ReadOnly = true)]
    [Description(
        "Lists the files of a directory, or of the top of a container, by name. Each file has its id, size, type, url " +
        "(set when the container is public), the directory it is in (directoryId), the user it is counted for " +
        "(creatorId), the client that stored it (clientId) and isAddressedByName: true for a file an application " +
        "saved under that name.")]
    [Authorize]
    public virtual Task<PagedResultDto<BlobManagementFileDto>> ListFilesAsync(
        [Description("A container's name, from blob_list_containers.")]
        string containerName,
        [Description("The directory, by id from blob_list_directories. Omit for the top of the container.")]
        Guid? directoryId = null,
        [Description("A part of the file name.")]
        string? filter = null,
        [Description("How many to skip, for paging.")]
        int skipCount = 0,
        [Description("How many to return, at most 100. Keep this small unless you need more.")]
        int maxResultCount = 20)
    {
        return FileAppService.GetListAsync(new GetBlobManagementFilesInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            Filter = filter,
            SkipCount = Math.Max(skipCount, 0),
            MaxResultCount = Math.Clamp(maxResultCount, 1, 100)
        });
    }

    [McpServerTool(Name = "blob_get_file", Title = "Get a file", ReadOnly = true)]
    [Description("Returns one file's details.")]
    [Authorize]
    public virtual Task<BlobManagementFileDto> GetFileAsync(
        [Description("The file's id, from blob_list_files.")]
        Guid id)
    {
        return FileAppService.GetAsync(id);
    }
}
