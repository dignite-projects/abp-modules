using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace Dignite.FileExplorer.Mcp;

/// <summary>
/// Fills in <see cref="FileDescriptorDto.Url"/>, which the application service leaves empty - over HTTP
/// the controller sets it from the request, and a file handed to a model without one cannot be referenced
/// anywhere.
/// <para>
/// Same format as the controller's, from the same route constant
/// (<see cref="FileExplorerRemoteServiceConsts.FilesRoutePrefix"/>), so a URL from either surface is
/// served by the HTTP API's file endpoint.
/// </para>
/// </summary>
public class FileExplorerMcpFileUrlBuilder : ITransientDependency
{
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public FileExplorerMcpFileUrlBuilder(IHttpContextAccessor httpContextAccessor)
    {
        HttpContextAccessor = httpContextAccessor;
    }

    public virtual FileDescriptorDto WithUrl(FileDescriptorDto file)
    {
        var request = HttpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            file.Url = $"{request.Scheme}://{request.Host.Value}/{FileExplorerRemoteServiceConsts.FilesRoutePrefix}/{file.ContainerName}/{file.BlobName}?__tenant={file.TenantId}";
        }

        return file;
    }
}
