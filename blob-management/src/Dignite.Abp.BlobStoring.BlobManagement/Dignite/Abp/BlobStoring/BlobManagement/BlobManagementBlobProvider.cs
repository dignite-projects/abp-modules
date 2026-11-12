using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement;
using Dignite.Abp.BlobManagement.Blobs;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;

namespace Dignite.Abp.BlobStoring.BlobManagement;

/// <summary>
/// An ABP BLOB provider that keeps a container's bytes in the Blob Management (<see cref="BlobManagementBlobContainerConfigurationExtensions.UseBlobManagement"/>).
/// It hands each call to <see cref="IBlobManagementBlobService"/> in the tenant ABP's container runs it under, and turns the
/// BlobManagement's <see cref="BlobManagementErrorCodes.BlobAlreadyExists"/> back into ABP's <see cref="BlobAlreadyExistsException"/>.
/// </summary>
public class BlobManagementBlobProvider : BlobProviderBase, ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Resolved on first use, not in the constructor: ABP builds every registered provider to select one, so a host
    /// that has not chosen an implementation yet should fail where Blob Management is used, with a message saying what to
    /// install.
    /// </summary>
    protected IBlobManagementBlobService BlobManagementBlobService => _driveBlobService ??=
        ServiceProvider.GetService(typeof(IBlobManagementBlobService)) as IBlobManagementBlobService
        ?? throw new AbpException(
            "No IBlobManagementBlobService is registered. The host installs Dignite.Abp.BlobManagement.Application (the BlobManagement in this process) or Dignite.Abp.BlobManagement.HttpApi.Client (a remote BlobManagement).");

    private IBlobManagementBlobService? _driveBlobService;

    public BlobManagementBlobProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var driveContainer = GetBlobManagementContainer(args);

        // The stream stays the caller's (ABP disposes what its pipeline created).
        var content = new RemoteStreamContent(args.BlobStream, fileName: args.BlobName, disposeStream: false);
        try
        {
            await BlobManagementBlobService.SaveAsync(driveContainer, args.BlobName, content, args.OverrideExisting);
        }
        catch (Exception exception) when (exception is IHasErrorCode { Code: BlobManagementErrorCodes.BlobAlreadyExists })
        {
            throw new BlobAlreadyExistsException(
                $"Saving BLOB '{args.BlobName}' does already exists in the container '{args.ContainerName}'! Set {nameof(args.OverrideExisting)} if it should be overwritten.",
                exception);
        }
    }

    public override Task<bool> DeleteAsync(BlobProviderDeleteArgs args)
    {
        return BlobManagementBlobService.DeleteAsync(GetBlobManagementContainer(args), args.BlobName);
    }

    public override Task<bool> ExistsAsync(BlobProviderExistsArgs args)
    {
        return BlobManagementBlobService.ExistsAsync(GetBlobManagementContainer(args), args.BlobName);
    }

    public override async Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
    {
        var content = await BlobManagementBlobService.GetOrNullAsync(GetBlobManagementContainer(args), args.BlobName);
        return content?.GetStream();
    }

    protected virtual string GetBlobManagementContainer(BlobProviderArgs args)
    {
        var configuration = args.Configuration.GetBlobManagementConfiguration();
        configuration.Validate();
        return configuration.BlobManagementContainer;
    }
}
