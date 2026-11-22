using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The BLOB API's <see cref="IBlobManagementBlobService"/>, an ABP integration service: every call authorizes its caller
/// first (<see cref="BlobManagementClientChecker"/>: an OAuth client granted the container's files, never a user), and
/// a BLOB it stores records the client as its writer. Exposed only through
/// <c>BlobManagementBlobIntegrationController</c>, and only where the host exposes integration services.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IBlobManagementBlobIntegrationService), typeof(BlobManagementBlobIntegrationService))]
public class BlobManagementBlobIntegrationService : BlobManagementBlobServiceBase, IBlobManagementBlobIntegrationService
{
    protected BlobManagementClientChecker ClientChecker { get; }

    public BlobManagementBlobIntegrationService(
        FileDescriptorManager fileDescriptorManager,
        BlobManagementClientChecker clientChecker)
        : base(fileDescriptorManager)
    {
        ClientChecker = clientChecker;
    }

    public virtual async Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null, bool isPublic = false, string? sha256 = null)
    {
        var clientId = await ClientChecker.CheckAsync(containerName);
        return await SaveBlobAsync(containerName, blobName, content, overrideExisting, creatorId, isPublic, sha256, clientId);
    }

    /// <summary>Fails with <see cref="BlobManagementErrorCodes.BlobNotFound"/> when there is no such file.</summary>
    public virtual async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        await ClientChecker.CheckAsync(containerName);
        return await OpenBlobOrNullAsync(containerName, blobName)
               ?? throw new BusinessException(BlobManagementErrorCodes.BlobNotFound)
                   .WithData("ContainerName", containerName)
                   .WithData("BlobName", blobName);
    }

    public virtual async Task<bool> ExistsAsync(string containerName, string blobName)
    {
        await ClientChecker.CheckAsync(containerName);
        return await BlobExistsAsync(containerName, blobName);
    }

    public virtual async Task<bool> DeleteAsync(string containerName, string blobName)
    {
        await ClientChecker.CheckAsync(containerName);
        return await DeleteBlobAsync(containerName, blobName);
    }
}
