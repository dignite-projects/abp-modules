namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// <see cref="IBlobManagementBlobService"/> for remote callers: what the BLOB API (<c>api/blob-management/blobs</c>) runs. It authorizes
/// the caller first - an OAuth client, never a user, allowed in the container's configuration - and reports a missing
/// file with <see cref="BlobManagementErrorCodes.BlobNotFound"/> rather than <c>null</c>, which HTTP cannot carry.
/// <para>
/// A separate contract from <see cref="IBlobManagementBlobService"/> because Blob Management's own host has both: the provider in
/// that host calls the trusted in-process <see cref="IBlobManagementBlobService"/>, the API calls this one.
/// </para>
/// </summary>
public interface IBlobManagementBlobAppService : IBlobManagementBlobService
{
}
