using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive.Blobs;

/// <summary>
/// The BLOB API's <see cref="IDriveBlobService"/>: the in-process implementation behind an authorization of the caller
/// (<see cref="DriveClientChecker"/>: an allowed OAuth client, never a user). The writer is recorded as the client. Exposed only through <c>DriveBlobController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IDriveBlobAppService), typeof(DriveBlobAppService))]
public class DriveBlobAppService : DriveBlobService, IDriveBlobAppService
{
    protected DriveClientChecker ClientChecker { get; }

    public DriveBlobAppService(FileDescriptorManager fileDescriptorManager, DriveClientChecker clientChecker)
        : base(fileDescriptorManager)
    {
        ClientChecker = clientChecker;
    }

    public override async Task<DriveBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false)
    {
        var clientId = await CheckClientAsync(containerName);
        return await SaveAsync(containerName, blobName, content, overrideExisting, clientId);
    }

    /// <summary>Fails with <see cref="DriveErrorCodes.BlobNotFound"/> when there is no such file.</summary>
    public override async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.GetOrNullAsync(containerName, blobName)
               ?? throw new BusinessException(DriveErrorCodes.BlobNotFound)
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

    /// <summary>The calling client's id, once it may use <paramref name="containerName"/> (<see cref="DriveClientChecker"/>).</summary>
    protected virtual Task<string> CheckClientAsync(string containerName)
    {
        return ClientChecker.CheckAsync(containerName);
    }
}
