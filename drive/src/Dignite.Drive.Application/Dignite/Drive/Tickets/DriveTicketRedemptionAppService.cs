using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;

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

    public DriveTicketRedemptionAppService(
        FileDescriptorManager fileDescriptorManager,
        DriveTicketProtector ticketProtector,
        IDriveClientAuthorizer clientAuthorizer)
    {
        FileDescriptorManager = fileDescriptorManager;
        TicketProtector = ticketProtector;
        ClientAuthorizer = clientAuthorizer;
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

    protected virtual BusinessException Invalid(string reason)
    {
        Logger.LogInformation("A Drive ticket was refused: {Reason}.", reason);
        return new BusinessException(DriveErrorCodes.InvalidTicket);
    }
}
