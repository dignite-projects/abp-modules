namespace Dignite.Drive.Blobs;

/// <summary>
/// <see cref="IDriveBlobService"/> for remote callers: what the BLOB API (<c>api/drive/blobs</c>) runs. It authorizes
/// the caller first - an OAuth client, never a user, allowed in the container's configuration - and reports a missing
/// file with <see cref="DriveErrorCodes.BlobNotFound"/> rather than <c>null</c>, which HTTP cannot carry.
/// <para>
/// A separate contract from <see cref="IDriveBlobService"/> because the Drive's own host has both: the provider in
/// that host calls the trusted in-process <see cref="IDriveBlobService"/>, the API calls this one.
/// </para>
/// </summary>
public interface IDriveBlobAppService : IDriveBlobService
{
}
