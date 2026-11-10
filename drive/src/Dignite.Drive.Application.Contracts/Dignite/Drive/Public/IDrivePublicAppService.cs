using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Drive.Public;

/// <summary>
/// Anonymous reads of the files in a <c>PublicRead</c> container (<see cref="DrivePublicUrl"/>), in the request's
/// tenant. Any other container, a missing file and a deleted one are all simply not found.
/// </summary>
public interface IDrivePublicAppService : IApplicationService
{
    /// <summary>What is behind the address, without its bytes: enough to answer a conditional request.</summary>
    Task<DrivePublicFileDto> GetInfoAsync(string containerName, string blobName);

    /// <summary>The file, or the picture resized when <paramref name="input"/> asks for a size.</summary>
    Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, DrivePublicReadInput input);
}
