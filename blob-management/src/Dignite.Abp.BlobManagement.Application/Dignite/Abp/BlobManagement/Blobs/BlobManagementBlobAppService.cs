using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The BLOB API's <see cref="IBlobManagementBlobService"/>: the in-process implementation behind an authorization of the caller
/// (<see cref="BlobManagementClientChecker"/>: an allowed OAuth client, never a user). The writer is recorded as the client. Exposed only through <c>BlobManagementBlobController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IBlobManagementBlobAppService), typeof(BlobManagementBlobAppService))]
public class BlobManagementBlobAppService : BlobManagementBlobService, IBlobManagementBlobAppService
{
    protected BlobManagementClientChecker ClientChecker { get; }

    public BlobManagementBlobAppService(FileDescriptorManager fileDescriptorManager, BlobManagementClientChecker clientChecker)
        : base(fileDescriptorManager)
    {
        ClientChecker = clientChecker;
    }

    public override async Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false)
    {
        var clientId = await CheckClientAsync(containerName);
        return await SaveAsync(containerName, blobName, content, overrideExisting, clientId);
    }

    /// <summary>Fails with <see cref="BlobManagementErrorCodes.BlobNotFound"/> when there is no such file.</summary>
    public override async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.GetOrNullAsync(containerName, blobName)
               ?? throw new BusinessException(BlobManagementErrorCodes.BlobNotFound)
                   .WithData("ContainerName", containerName)
                   .WithData("BlobName", blobName);
    }

    public override async Task<bool> ExistsAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.ExistsAsync(containerName, blobName);
    }

    public override async Task<bool> DeleteAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.DeleteAsync(containerName, blobName);
    }

    /// <summary>The calling client's id, once it may use <paramref name="containerName"/> (<see cref="BlobManagementClientChecker"/>).</summary>
    protected virtual Task<string> CheckClientAsync(string containerName)
    {
        return ClientChecker.CheckAsync(containerName);
    }
}
