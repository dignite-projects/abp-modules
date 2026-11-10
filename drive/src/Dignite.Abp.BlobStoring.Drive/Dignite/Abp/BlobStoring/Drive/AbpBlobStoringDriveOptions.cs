namespace Dignite.Abp.BlobStoring.Drive;

public class AbpBlobStoringDriveOptions
{
    /// <summary>
    /// Where the Drive's public reads are reached from the outside, put in front of every address
    /// <see cref="IDrivePublicUrlBuilder"/> builds: the Drive service's or the gateway's address
    /// (<c>https://drive.example.com</c>) when the Drive runs on its own. Empty (the default) builds addresses relative
    /// to the application's own origin, right when the Drive is installed in the same host.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
