using Volo.Abp;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// <see cref="IBlobManagementFileService"/> as an ABP integration service: what the file API
/// (<c>integration-api/blob-management/files</c>) runs, for a business system's back end. It authorizes the caller
/// first: an OAuth client granted the container's files, with a token that carries no user - for every call. A file in
/// a container the client may not use is not found, so its id tells nothing.
/// <para>
/// As any integration service, it is not exposed over HTTP unless the host says so
/// (<c>AbpAspNetCoreMvcOptions.ExposeIntegrationServices</c>): a host that installs Blob Management in its own process
/// keeps files through the trusted <see cref="IBlobManagementFileService"/> and exposes nothing; a Blob Management that
/// runs as a service of its own exposes it, to the back ends that call it - never to browsers. What a user reads at
/// the management page is <see cref="IBlobManagementFileAppService"/>.
/// </para>
/// <para>
/// A separate contract from <see cref="IBlobManagementFileService"/> because Blob Management's own host has both: an
/// application in that host calls the trusted in-process <see cref="IBlobManagementFileService"/>, the API calls this
/// one.
/// </para>
/// </summary>
[IntegrationService]
public interface IBlobManagementFileIntegrationService : IBlobManagementFileService
{
}
