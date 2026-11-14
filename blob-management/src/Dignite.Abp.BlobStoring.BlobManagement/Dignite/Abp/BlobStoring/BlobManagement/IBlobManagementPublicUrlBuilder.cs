namespace Dignite.Abp.BlobStoring.BlobManagement;

/// <summary>
/// The public address of a file an application stored through the Blob Management provider, in a container it declared
/// public (<see cref="BlobManagementBlobProviderConfiguration.IsPublic"/>): the address answers while the file is
/// public, and 404 otherwise.
/// </summary>
public interface IBlobManagementPublicUrlBuilder
{
    /// <summary>
    /// The address of <paramref name="blobName"/> in the application's container <paramref name="containerName"/> (one
    /// configured with <c>UseBlobManagement</c>): <see cref="AbpBlobStoringBlobManagementOptions.PublicBaseUrl"/> followed by
    /// <c>/blobs/{container}/{blobName}</c>, with <c>?__tenant=</c> when the file belongs to a tenant
    /// and the size asked, if any. A width or height must be one the Blob Management host allows - by default 64, 128, 256, 384,
    /// 512, 768, 1024, 1536, 2048 or 4096 - otherwise the address answers 400. The builder does not check: Blob Management may
    /// be a remote one whose list this code cannot know.
    /// </summary>
    string Build(string containerName, string blobName, int? width = null, int? height = null);
}
