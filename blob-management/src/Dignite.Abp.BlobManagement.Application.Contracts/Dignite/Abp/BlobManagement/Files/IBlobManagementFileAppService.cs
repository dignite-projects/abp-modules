namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// <see cref="IBlobManagementFileService"/> for remote callers: what <c>api/blob-management/files</c> runs. It
/// authorizes the caller first, and admits two:
/// <list type="bullet">
/// <item>an OAuth client granted the container's files, with a token that carries no user - for everything;</item>
/// <item>a user who holds the management permission - for the reads, which is how the management page shows a
/// container's files.</item>
/// </list>
/// <para>
/// A separate contract from <see cref="IBlobManagementFileService"/> because Blob Management's own host has both: an
/// application in that host calls the trusted in-process <see cref="IBlobManagementFileService"/>, the API calls this
/// one.
/// </para>
/// </summary>
public interface IBlobManagementFileAppService : IBlobManagementFileService
{
}
