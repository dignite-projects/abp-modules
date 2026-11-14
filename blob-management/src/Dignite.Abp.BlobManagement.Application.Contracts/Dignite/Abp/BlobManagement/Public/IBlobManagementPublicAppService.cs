using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Public;

/// <summary>
/// Anonymous reads of the files marked public (<see cref="BlobManagementPublicUrl"/>), in the request's tenant. A file
/// that is not public, a missing or deleted one and a container that is not managed are all simply not found.
/// </summary>
public interface IBlobManagementPublicAppService : IApplicationService
{
    /// <summary>
    /// Reads the file at the address once: its information and entity tag, whether the client's copy is current, and -
    /// unless it is, or <paramref name="request"/> wants headers only - its bytes, or the picture resized when
    /// <paramref name="input"/> asks for a size. Everything in the result comes from the same lookup of the file, so a
    /// file overwritten meanwhile never mixes the old one's tag or type with the new one's bytes.
    /// <para>
    /// A width or height must be one the Blob Management allows (<c>BlobManagementPublicReadOptions.AllowedSizes</c>), otherwise
    /// <see cref="Volo.Abp.Validation.AbpValidationException"/>, checked before the lookup. A missing file - or a file
    /// whose bytes are missing from storage - is <see cref="Volo.Abp.Domain.Entities.EntityNotFoundException"/>.
    /// </para>
    /// </summary>
    Task<BlobManagementPublicReadResult> ReadAsync(
        string containerName,
        string blobName,
        BlobManagementPublicReadInput input,
        BlobManagementPublicReadRequest request);
}
