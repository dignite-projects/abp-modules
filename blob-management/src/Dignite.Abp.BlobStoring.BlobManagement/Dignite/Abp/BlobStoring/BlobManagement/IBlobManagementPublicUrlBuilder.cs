namespace Dignite.Abp.BlobStoring.BlobManagement;

/// <summary>
/// The public address of a file an application stored through the Blob Management provider, for a container whose Blob Management container
/// is read publicly (<c>PublicRead</c>, set on the Blob Management host - the application cannot see it).
/// </summary>
public interface IBlobManagementPublicUrlBuilder
{
    /// <summary>
    /// The address of <paramref name="blobName"/> in the application's container <paramref name="containerName"/> (one
    /// configured with <c>UseBlobManagement</c>): <see cref="AbpBlobStoringBlobManagementOptions.PublicBaseUrl"/> followed by
    /// <c>/api/blob-management/public/{container}/{blobName}</c>, with <c>?__tenant=</c> when the file belongs to a tenant
    /// and the size asked, if any. A width or height must be one the Blob Management host allows - by default 64, 128, 256, 384,
    /// 512, 768, 1024, 1536, 2048 or 4096 - otherwise the address answers 400. The builder does not check: Blob Management may
    /// be a remote one whose list this code cannot know.
    /// </summary>
    string Build(string containerName, string blobName, int? width = null, int? height = null);
}
