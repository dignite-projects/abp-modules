using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Drive.Public;

/// <summary>
/// Anonymous reads of the files in a <c>PublicRead</c> container (<see cref="DrivePublicUrl"/>), in the request's
/// tenant. Any other container, a missing file and a deleted one are all simply not found.
/// </summary>
public interface IDrivePublicAppService : IApplicationService
{
    /// <summary>
    /// Reads the file at the address once: its information and entity tag, whether the client's copy is current, and -
    /// unless it is, or <paramref name="request"/> wants headers only - its bytes, or the picture resized when
    /// <paramref name="input"/> asks for a size. Everything in the result comes from the same lookup of the file, so a
    /// file overwritten meanwhile never mixes the old one's tag or type with the new one's bytes.
    /// <para>
    /// A width or height must be one the Drive allows (<c>DrivePublicReadOptions.AllowedSizes</c>), otherwise
    /// <see cref="Volo.Abp.Validation.AbpValidationException"/>, checked before the lookup. A missing file - or a file
    /// whose bytes are missing from storage - is <see cref="Volo.Abp.Domain.Entities.EntityNotFoundException"/>.
    /// </para>
    /// </summary>
    Task<DrivePublicReadResult> ReadAsync(
        string containerName,
        string blobName,
        DrivePublicReadInput input,
        DrivePublicReadRequest request);
}
