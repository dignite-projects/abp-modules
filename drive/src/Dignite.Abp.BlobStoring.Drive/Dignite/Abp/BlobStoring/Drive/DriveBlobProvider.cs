using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Drive;
using Dignite.Drive.Blobs;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;

namespace Dignite.Abp.BlobStoring.Drive;

/// <summary>
/// An ABP BLOB provider that keeps a container's bytes in the Drive (<see cref="DriveBlobContainerConfigurationExtensions.UseDrive"/>).
/// It hands each call to <see cref="IDriveBlobService"/> in the tenant ABP's container runs it under, and turns the
/// Drive's <see cref="DriveErrorCodes.BlobAlreadyExists"/> back into ABP's <see cref="BlobAlreadyExistsException"/>.
/// </summary>
public class DriveBlobProvider : BlobProviderBase, ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Resolved on first use, not in the constructor: ABP builds every registered provider to select one, so a host
    /// that has not chosen an implementation yet should fail where the Drive is used, with a message saying what to
    /// install.
    /// </summary>
    protected IDriveBlobService DriveBlobService => _driveBlobService ??=
        ServiceProvider.GetService(typeof(IDriveBlobService)) as IDriveBlobService
        ?? throw new AbpException(
            "No IDriveBlobService is registered. The host installs Dignite.Drive.Application (the Drive in this process) or Dignite.Drive.HttpApi.Client (a remote Drive).");

    private IDriveBlobService? _driveBlobService;

    public DriveBlobProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var driveContainer = GetDriveContainer(args);

        // The stream stays the caller's (ABP disposes what its pipeline created).
        var content = new RemoteStreamContent(args.BlobStream, fileName: args.BlobName, disposeStream: false);
        try
        {
            await DriveBlobService.SaveAsync(driveContainer, args.BlobName, content, args.OverrideExisting);
        }
        catch (Exception exception) when (exception is IHasErrorCode { Code: DriveErrorCodes.BlobAlreadyExists })
        {
            throw new BlobAlreadyExistsException(
                $"Saving BLOB '{args.BlobName}' does already exists in the container '{args.ContainerName}'! Set {nameof(args.OverrideExisting)} if it should be overwritten.",
                exception);
        }
    }

    public override Task<bool> DeleteAsync(BlobProviderDeleteArgs args)
    {
        return DriveBlobService.DeleteAsync(GetDriveContainer(args), args.BlobName);
    }

    public override Task<bool> ExistsAsync(BlobProviderExistsArgs args)
    {
        return DriveBlobService.ExistsAsync(GetDriveContainer(args), args.BlobName);
    }

    public override async Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
    {
        var content = await DriveBlobService.GetOrNullAsync(GetDriveContainer(args), args.BlobName);
        return content?.GetStream();
    }

    protected virtual string GetDriveContainer(BlobProviderArgs args)
    {
        var configuration = args.Configuration.GetDriveConfiguration();
        configuration.Validate();
        return configuration.DriveContainer;
    }
}
