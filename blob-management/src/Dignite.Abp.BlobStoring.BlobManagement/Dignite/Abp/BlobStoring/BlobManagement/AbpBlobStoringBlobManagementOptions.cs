namespace Dignite.Abp.BlobStoring.BlobManagement;

public class AbpBlobStoringBlobManagementOptions
{
    /// <summary>
    /// Where the BlobManagement's public reads are reached from the outside, put in front of every address
    /// <see cref="IBlobManagementPublicUrlBuilder"/> builds: the Blob Management service's or the gateway's address
    /// (<c>https://blobs.example.com</c>) when Blob Management runs on its own. Empty (the default) builds addresses relative
    /// to the application's own origin, right when Blob Management is installed in the same host.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
