using System.Threading.Tasks;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// Decides whether an OAuth client may use a Blob Management container through the BLOB API. The default
/// (<see cref="AllowListBlobManagementClientAuthorizer"/>) reads the container's allowed client ids; another source - ABP's
/// <c>C</c>-provider resource grants, say - can be added by replacing this service with one that allows when either
/// source allows.
/// </summary>
public interface IBlobManagementClientAuthorizer
{
    /// <summary>Whether <paramref name="clientId"/> may read, write and delete in <paramref name="containerName"/>.</summary>
    Task<bool> IsAllowedAsync(string containerName, string clientId);
}
