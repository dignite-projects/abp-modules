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
using Volo.Abp.Threading;

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
    protected IBlobManagementBlobService BlobManagementBlobService => _blobService ??=
        ServiceProvider.GetService(typeof(IBlobManagementBlobService)) as IBlobManagementBlobService
        ?? throw new AbpException(
            "No IBlobManagementBlobService is registered. The host installs Dignite.Abp.BlobManagement.Application (Blob Management in this process) or Dignite.Abp.BlobManagement.HttpApi.Client (a remote Blob Management).");

    private IBlobManagementBlobService? _blobService;

    /// <summary>
    /// Carries the caller's cancellation token to the service, which - an application service - takes none as an
    /// argument: each call runs with <c>args.CancellationToken</c> as ABP's ambient token
    /// (<see cref="ICancellationTokenProvider.Use"/>), which the in-process service hands on and ABP's HTTP client
    /// proxies read. ABP's container has already fallen back to the ambient token when the caller passed none, so
    /// this never replaces one with nothing.
    /// </summary>
    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    public BlobManagementBlobProvider(IServiceProvider serviceProvider, ICancellationTokenProvider cancellationTokenProvider)
    {
        ServiceProvider = serviceProvider;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var managedContainer = GetBlobManagementContainer(args);
        var isPublic = args.Configuration.GetBlobManagementConfiguration().IsPublic;

        // The stream stays the caller's (ABP disposes what its pipeline created).
        var content = new RemoteStreamContent(args.BlobStream, fileName: args.BlobName, disposeStream: false);
        try
        {
            using (CancellationTokenProvider.Use(args.CancellationToken))
            {
                await BlobManagementBlobService.SaveAsync(managedContainer, args.BlobName, content, args.OverrideExisting, isPublic: isPublic);
            }
        }
        catch (Exception exception) when (exception is IHasErrorCode { Code: BlobManagementErrorCodes.BlobAlreadyExists })
        {
            throw new BlobAlreadyExistsException(
                $"Saving BLOB '{args.BlobName}' does already exists in the container '{args.ContainerName}'! Set {nameof(args.OverrideExisting)} if it should be overwritten.",
                exception);
        }
    }

    public override async Task<bool> DeleteAsync(BlobProviderDeleteArgs args)
    {
        using (CancellationTokenProvider.Use(args.CancellationToken))
        {
            return await BlobManagementBlobService.DeleteAsync(GetBlobManagementContainer(args), args.BlobName);
        }
    }

    public override async Task<bool> ExistsAsync(BlobProviderExistsArgs args)
    {
        using (CancellationTokenProvider.Use(args.CancellationToken))
        {
            return await BlobManagementBlobService.ExistsAsync(GetBlobManagementContainer(args), args.BlobName);
        }
    }

    public override async Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
    {
        using (CancellationTokenProvider.Use(args.CancellationToken))
        {
            var content = await BlobManagementBlobService.GetOrNullAsync(GetBlobManagementContainer(args), args.BlobName);
            return content?.GetStream();
        }
    }

    /// <summary>The managed container the bytes go to, which <c>UseBlobManagement</c> required.</summary>
    protected virtual string GetBlobManagementContainer(BlobProviderArgs args)
    {
        return args.Configuration.GetBlobManagementConfiguration().BlobManagementContainer;
    }
}
