namespace Dignite.Abp.BlobStoring.Drive;

/// <summary>
/// The public address of a file an application stored through the Drive provider, for a container whose Drive container
/// is read publicly (<c>PublicRead</c>, set on the Drive host - the application cannot see it).
/// </summary>
public interface IDrivePublicUrlBuilder
{
    /// <summary>
    /// The address of <paramref name="blobName"/> in the application's container <paramref name="containerName"/> (one
    /// configured with <c>UseDrive</c>): <see cref="AbpBlobStoringDriveOptions.PublicBaseUrl"/> followed by
    /// <c>/api/drive/public/{driveContainer}/{blobName}</c>, with <c>?__tenant=</c> when the file belongs to a tenant
    /// and the size asked, if any.
    /// </summary>
    string Build(string containerName, string blobName, int? width = null, int? height = null);
}
