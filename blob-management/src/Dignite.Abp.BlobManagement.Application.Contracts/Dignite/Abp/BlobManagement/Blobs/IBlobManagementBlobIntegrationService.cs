using Volo.Abp;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// <see cref="IBlobManagementBlobService"/> as an ABP integration service: what the BLOB API
/// (<c>integration-api/blob-management/blobs</c>) runs, for an application's back end. It authorizes the caller first -
/// an OAuth client, never a user, granted the container's files - and reports a missing file with
/// <see cref="BlobManagementErrorCodes.BlobNotFound"/> rather than <c>null</c>, which HTTP cannot carry.
/// <para>
/// As any integration service, it is not exposed over HTTP unless the host says so
/// (<c>AbpAspNetCoreMvcOptions.ExposeIntegrationServices</c>): a host that installs Blob Management in its own process
/// stores through the trusted <see cref="IBlobManagementBlobService"/> and exposes nothing; a Blob Management that runs
/// as a service of its own exposes it, to the back ends that call it - never to browsers.
/// </para>
/// <para>
/// A separate contract from <see cref="IBlobManagementBlobService"/> because Blob Management's own host has both: the
/// provider in that host calls the trusted in-process <see cref="IBlobManagementBlobService"/>, the API calls this one.
/// </para>
/// </summary>
[IntegrationService]
public interface IBlobManagementBlobIntegrationService : IBlobManagementBlobService
{
}
