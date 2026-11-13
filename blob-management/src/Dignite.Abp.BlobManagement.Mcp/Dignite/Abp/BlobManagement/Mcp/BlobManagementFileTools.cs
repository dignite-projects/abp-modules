using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// Listing, adding, reading, renaming, moving and deleting files. Each tool calls the file application service, which
/// holds the authorization and the container's upload rules (size, type detected from the bytes, image processing);
/// none of it is repeated here.
/// </summary>
[McpServerToolType]
public class BlobManagementFileTools : ITransientDependency
{
    protected IBlobManagementFileAppService FileAppService { get; }

    protected IBlobManagementContainerAppService ContainerAppService { get; }

    protected BlobManagementMcpOptions Options { get; }

    /// <summary>Where the request being served came in; absent outside a web request.</summary>
    protected IHttpContextAccessor? HttpContextAccessor { get; }

    public BlobManagementFileTools(
        IBlobManagementFileAppService fileAppService,
        IBlobManagementContainerAppService containerAppService,
        IOptions<BlobManagementMcpOptions> options,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        FileAppService = fileAppService;
        ContainerAppService = containerAppService;
        Options = options.Value;
        HttpContextAccessor = httpContextAccessor;
    }

    [McpServerTool(Name = "blob_list_files", Title = "List files", ReadOnly = true)]
    [Description(
        "Lists the files of a directory, or of the top of a container, by name. Each file has its id, size, type, url " +
        "(set when the container is public), the directory it is in (directoryId) and isAddressedByName: true for a file " +
        "an application saved under that name, which cannot be renamed or moved.")]
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
        [Description("The file's id, as an upload returned it.")]
        Guid id)
    {
        return FileAppService.GetAsync(id);
    }

    [McpServerTool(Name = "blob_create_upload_url", Title = "Get a link to upload a file to")]
    [Description(
        "The way to add a real file - a document, a photo: returns a one-time link, good for ten minutes, that the file is " +
        "posted to, and the command that does it. The bytes go from the machine the file is on to Blob Management and never " +
        "through you, so a large file costs nothing here; run the command on that machine (replace FILE_PATH): command for " +
        "sh, bash, zsh and cmd.exe, powerShellCommand for PowerShell. Its output is the stored file. The container's rules " +
        "apply (its file types and size; the type is detected from the bytes, so the extension of fileName must match " +
        "them), checked now for the link and again, with your permissions then, when the file arrives.")]
    [Authorize]
    public virtual async Task<BlobManagementMcpUploadUrl> CreateUploadUrlAsync(
        [Description("A container's name, from blob_list_containers.")]
        string containerName,
        [Description("The name the file is stored under, with its extension, e.g. 'minutes.pdf'.")]
        string fileName,
        [Description("The directory to file it in, by id from blob_list_directories. Omit for the top of the container.")]
        Guid? directoryId = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw Invalid(nameof(fileName), "A file name is required.");
        }

        var ticket = await FileAppService.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            FileName = fileName
        });

        var url = GetBaseUrl().TrimEnd('/') + ticket.Url;
        return new BlobManagementMcpUploadUrl
        {
            Url = url,
            ExpiresAt = ticket.ExpiresAt,
            ContainerName = containerName,
            FileName = fileName.Trim(),
            Command = $"curl -sS --fail-with-body -F \"content=@FILE_PATH\" \"{url}\"",
            PowerShellCommand = $"curl.exe -sS --fail-with-body -F \"content=@FILE_PATH\" \"{url}\""
        };
    }

    [McpServerTool(Name = "blob_upload_file", Title = "Upload a small file")]
    [Description(
        "Adds a file from base64-encoded bytes. Small files only: the bytes are text you have to write out, so this suits " +
        "a file of a few kilobytes; blob_create_upload_url is the way to send anything larger. The container's rules apply, " +
        "and the type is detected from the bytes, so the file name's extension must match the content.")]
    [Authorize]
    public virtual async Task<BlobManagementFileDto> UploadFileAsync(
        [Description("A container's name, from blob_list_containers.")]
        string containerName,
        [Description("The file name, with its extension, e.g. 'note.txt'.")]
        string fileName,
        [Description("The file's bytes, base64-encoded. A data: URL is also accepted.")]
        string contentBase64,
        [Description("The directory to file it in, by id from blob_list_directories. Omit for the top of the container.")]
        Guid? directoryId = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw Invalid(nameof(fileName), "A file name is required.");
        }

        var container = await ContainerAppService.GetAsync(containerName);
        var bytes = Decode(contentBase64, Math.Min(Options.MaxInlineUploadSize, container.MaxFileSize));

        return await FileAppService.CreateAsync(new CreateBlobManagementFileInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            Name = fileName,
            // Never used: Blob Management detects the type from the bytes.
            File = new RemoteStreamContent(new MemoryStream(bytes, writable: false), fileName, "application/octet-stream", bytes.LongLength)
        });
    }

    [McpServerTool(Name = "blob_update_file", Title = "Rename or move a file", Idempotent = true)]
    [Description(
        "Renames a file, moves it to another directory of the same container, or both. Only what you pass changes. A " +
        "file stays in its container. A file an application saved under its name (isAddressedByName) is neither renamed " +
        "nor moved.")]
    [Authorize]
    public virtual async Task<BlobManagementFileDto> UpdateFileAsync(
        [Description("The file's id.")]
        Guid id,
        [Description("The new file name. Omit to keep it.")]
        string? name = null,
        [Description("Move it into this directory, by id. Omit to leave it where it is.")]
        Guid? directoryId = null,
        [Description("True to move it to the top of its container. Cannot be combined with directoryId.")]
        bool moveToTop = false)
    {
        if (moveToTop && directoryId.HasValue)
        {
            throw Invalid(nameof(directoryId), "Pass either directoryId or moveToTop, not both.");
        }

        if (name == null && !directoryId.HasValue && !moveToTop)
        {
            throw Invalid(nameof(name), "Pass a new name, a directory to move it to, or moveToTop.");
        }

        // Two calls cannot be one transaction, so make the half-done outcome unlikely: refuse a name the rename would
        // refuse before anything changes, and move first - the step that also takes the right to add at the target.
        if (name != null && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > FileDescriptorConsts.MaxNameLength))
        {
            throw Invalid(nameof(name), $"The file name must not be empty or longer than {FileDescriptorConsts.MaxNameLength} characters.");
        }

        BlobManagementFileDto? file = null;
        if (directoryId.HasValue || moveToTop)
        {
            file = await FileAppService.MoveAsync(id, new MoveBlobManagementFileInput { DirectoryId = directoryId });
        }

        if (name != null)
        {
            file = await FileAppService.UpdateAsync(id, new UpdateBlobManagementFileInput { Name = name });
        }

        return file!;
    }

    [McpServerTool(Name = "blob_delete_file", Title = "Delete a file", Destructive = true)]
    [Description("Deletes a file. Anything that links to its url stops working.")]
    [Authorize]
    public virtual async Task<string> DeleteFileAsync(
        [Description("The file's id.")]
        Guid id)
    {
        var file = await FileAppService.GetAsync(id);
        await FileAppService.DeleteAsync(id);
        return $"Deleted '{file.Name}' ({id}).";
    }

    /// <summary>
    /// Where Blob Management is reached: <see cref="BlobManagementMcpOptions.BaseUrl"/>, else the scheme and host this MCP request came
    /// in on (with its path base), which is Blob Management's when the MCP server runs in its host.
    /// </summary>
    protected virtual string GetBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Options.BaseUrl))
        {
            return Options.BaseUrl!;
        }

        var request = HttpContextAccessor?.HttpContext?.Request
                      ?? throw new InvalidOperationException(
                          $"The address of an upload link is unknown: {nameof(BlobManagementMcpOptions)}.{nameof(BlobManagementMcpOptions.BaseUrl)} is not set and there is no request to take it from.");

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    /// <summary>
    /// Decodes an inline upload, refusing an oversized one from its encoded length before allocating the decoded copy.
    /// </summary>
    protected virtual byte[] Decode(string contentBase64, long maxSize)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            throw Invalid(nameof(contentBase64), "The file content is empty.");
        }

        var payload = contentBase64.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var marker = payload.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                throw Invalid(nameof(contentBase64), "A data: URL must be base64-encoded (';base64,').");
            }

            payload = payload[(marker + ";base64,".Length)..];
        }

        if (DecodedLength(payload) > maxSize)
        {
            throw TooLarge(maxSize);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw Invalid(nameof(contentBase64), "The content is not valid base64.");
        }

        if (bytes.Length == 0)
        {
            throw Invalid(nameof(contentBase64), "The file content is empty.");
        }

        if (bytes.LongLength > maxSize)
        {
            throw TooLarge(maxSize);
        }

        return bytes;
    }

    /// <summary>
    /// The number of bytes <paramref name="base64"/> decodes to, if it is valid: every 4 characters are 3 bytes, once the
    /// whitespace a decoder skips and the trailing padding are left out.
    /// </summary>
    protected static long DecodedLength(string base64)
    {
        long significant = 0;
        var padding = 0;
        foreach (var character in base64)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            significant++;
            padding = character == '=' ? padding + 1 : 0;
        }

        return significant / 4 * 3 - Math.Min(padding, 2);
    }

    protected static AbpValidationException TooLarge(long maxSize)
    {
        return Invalid(
            "contentBase64",
            $"The file is larger than {maxSize} bytes, the most blob_upload_file takes here. Use blob_create_upload_url.");
    }

    protected static AbpValidationException Invalid(string member, string message)
    {
        return new AbpValidationException(message, new List<ValidationResult> { new(message, new[] { member }) });
    }
}
