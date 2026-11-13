namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementClientUsageDto
{
    /// <summary>
    /// The OAuth client that stored the files over HTTP; <c>null</c> gathers the files stored in process, by an
    /// application in Blob Management's own host.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>The sizes of the client's files, whether or not they share their bytes with another file.</summary>
    public long UploadedBytes { get; set; }

    public long FileCount { get; set; }
}
