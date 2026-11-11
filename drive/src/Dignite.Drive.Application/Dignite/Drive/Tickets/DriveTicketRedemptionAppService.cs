using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Blobs;
using Dignite.Drive.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Redeems tickets for whoever holds one. The ticket is the authorization: it is read (signed by this Drive, not
/// expired, of the kind redeemed), its issuer is checked again (a client still allowed in the container - an in-process
/// issuer is trusted), and the work runs in the ticket's tenant, whatever the request's. Everything that makes a ticket
/// unusable answers <see cref="DriveErrorCodes.InvalidTicket"/>; the reason is logged, not told.
/// </summary>
[AllowAnonymous]
public class DriveTicketRedemptionAppService : DriveAppService, IDriveTicketRedemptionAppService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected DriveTicketProtector TicketProtector { get; }

    protected IDriveClientAuthorizer ClientAuthorizer { get; }

    protected DriveUsageManager DriveUsageManager { get; }

    public DriveTicketRedemptionAppService(
        FileDescriptorManager fileDescriptorManager,
        DriveTicketProtector ticketProtector,
        IDriveClientAuthorizer clientAuthorizer,
        DriveUsageManager driveUsageManager)
    {
        FileDescriptorManager = fileDescriptorManager;
        TicketProtector = ticketProtector;
        ClientAuthorizer = clientAuthorizer;
        DriveUsageManager = driveUsageManager;
    }

    public virtual async Task<DriveBlobDto> UploadAsync(string ticket, IRemoteStreamContent content)
    {
        Check.NotNull(content, nameof(content));
        var read = await ReadAsync(ticket, DriveTicketKind.Upload);

        using (CurrentTenant.Change(read.TenantId))
        {
            var (used, result) = await TicketProtector.UseOnceAsync(read, () => StoreAsync(read, content));
            if (!used)
            {
                throw Invalid("it has been used, or is being used");
            }

            return result!;
        }
    }

    public virtual async Task<DriveFileDto> UploadFileAsync(string ticket, IRemoteStreamContent content)
    {
        Check.NotNull(content, nameof(content));
        var read = await ReadAsync(ticket, DriveTicketKind.FileUpload);
        if (read.UserId == null || string.IsNullOrWhiteSpace(read.FileName))
        {
            throw Invalid("it names no user or file");
        }

        using (CurrentTenant.Change(read.TenantId))
        using (LazyServiceProvider.LazyGetRequiredService<ICurrentPrincipalAccessor>().Change(DriveTicketPrincipal.Create(read)))
        {
            // Before the ticket is used up: the file service is behind the Drive feature, and its authorization error
            // would be rewritten to InvalidTicket below, which tells the client the wrong thing. Disabled answers
            // DriveDisabled and leaves the ticket good for when the Drive is on again.
            await DriveUsageManager.CheckEnabledAsync();

            try
            {
                var (used, result) = await TicketProtector.UseOnceAsync(read, () => StoreFileAsync(read, content));
                if (!used)
                {
                    throw Invalid("it has been used, or is being used");
                }

                return result!;
            }
            catch (AbpAuthorizationException)
            {
                throw Invalid($"its issuer '{read.UserId}' may no longer add files there");
            }
        }
    }

    public virtual async Task<IRemoteStreamContent> DownloadAsync(string ticket)
    {
        var read = await ReadAsync(ticket, DriveTicketKind.Download);

        using (CurrentTenant.Change(read.TenantId))
        {
            var file = await FileDescriptorManager.FindAsync(read.ContainerName, read.BlobName);
            var stream = file == null ? null : await FileDescriptorManager.GetStreamOrNullAsync(file);
            if (file == null || stream == null)
            {
                throw new EntityNotFoundException(typeof(FileDescriptor), $"{read.ContainerName}/{read.BlobName}");
            }

            var slash = file.Name.LastIndexOf('/');
            return new RemoteStreamContent(stream, slash < 0 ? file.Name : file.Name.Substring(slash + 1), file.MimeType, file.Size);
        }
    }

    /// <summary>The ticket, when it can be redeemed as <paramref name="kind"/> now.</summary>
    protected virtual async Task<DriveTicket> ReadAsync(string ticket, DriveTicketKind kind)
    {
        var read = TicketProtector.Read(ticket);
        if (read == null)
        {
            throw Invalid("it is not signed by this Drive, or has expired");
        }

        if (read.Kind != kind)
        {
            throw Invalid($"it is a {read.Kind} ticket");
        }

        if (read.ClientId != null && !await ClientAuthorizer.IsAllowedAsync(read.ContainerName, read.ClientId))
        {
            throw Invalid($"its issuer '{read.ClientId}' may no longer use the container '{read.ContainerName}'");
        }

        return read;
    }

    protected virtual async Task<DriveBlobDto> StoreAsync(DriveTicket ticket, IRemoteStreamContent content)
    {
        // The ticket's own cap first, below the container's: BlobStreamBuffering fails with ContentTooLarge.
        var stream = content.GetStream();
        MemoryStream? capped = null;
        if (ticket.MaxSizeInBytes != null)
        {
            capped = await BlobStreamBuffering.CopyToBufferAsync(stream, ticket.MaxSizeInBytes.Value, default);
            stream = capped;
        }

        try
        {
            var file = await FileDescriptorManager.CreateAsync(
                ticket.ContainerName, ticket.BlobName, stream, ticket.OverrideExisting, ticket.ClientId);

            return new DriveBlobDto
            {
                Name = file.Name,
                MimeType = file.MimeType,
                Size = file.Size,
                Hash = file.Hash
            };
        }
        finally
        {
            if (capped != null)
            {
                await capped.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// The upload through the file API's own service, as the ticket's user: the drive and the permission are
    /// checked as for a user uploading with a token, and the name is the ticket's.
    /// </summary>
    protected virtual async Task<DriveFileDto> StoreFileAsync(DriveTicket ticket, IRemoteStreamContent content)
    {
        // Lent, not given: the content is the request's, so the file service must not close it.
        var lent = new RemoteStreamContent(
            content.GetStream(), ticket.FileName, content.ContentType, content.ContentLength, disposeStream: false);

        return await LazyServiceProvider.LazyGetRequiredService<IDriveFileAppService>().CreateAsync(new CreateDriveFileInput
        {
            ContainerName = ticket.ContainerName,
            Name = ticket.FileName,
            File = lent
        });
    }

    protected virtual BusinessException Invalid(string reason)
    {
        Logger.LogInformation("A Drive ticket was refused: {Reason}.", reason);
        return new BusinessException(DriveErrorCodes.InvalidTicket);
    }
}
