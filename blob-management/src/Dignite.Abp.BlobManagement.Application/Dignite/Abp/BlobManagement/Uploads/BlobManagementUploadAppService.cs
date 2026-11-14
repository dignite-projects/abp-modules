using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Tickets;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// The anonymous upload with a ticket. The ticket is the whole authorization: it names the tenant, the container, the
/// place, the file's id, the user and the client, the size and the types; the request brings the bytes and the file's
/// name, and nothing else of it is used - no signed-in user, no cookie, no tenant it names (the call runs under an
/// anonymous principal, in the ticket's tenant).
/// <para>
/// The file is stored as any file kept by id is (<see cref="FileDescriptorManager.UploadAsync"/>): the container's
/// pipeline contributors run, the tenant's allocation is checked. It is pending until its
/// business system confirms it. A ticket is used once: its file's id is given, and a second upload collides with the
/// first one's row - deleted rows included - and is refused before anything is stored; two at the same moment are
/// settled by the primary key, and the loser's bytes are removed again.
/// </para>
/// Exposed only through <c>BlobManagementUploadController</c>.
/// </summary>
[AllowAnonymous]
[RemoteService(IsEnabled = false)]
public class BlobManagementUploadAppService : BlobManagementAppService, IBlobManagementUploadAppService
{
    protected BlobManagementTicketProtector TicketProtector { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IFileDescriptorRepository FileRepository { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected ICurrentPrincipalAccessor PrincipalAccessor { get; }

    public BlobManagementUploadAppService(
        BlobManagementTicketProtector ticketProtector,
        FileDescriptorManager fileDescriptorManager,
        IFileDescriptorRepository fileRepository,
        IMimeTypeDetector mimeTypeDetector,
        ICurrentPrincipalAccessor principalAccessor)
    {
        TicketProtector = ticketProtector;
        FileDescriptorManager = fileDescriptorManager;
        FileRepository = fileRepository;
        MimeTypeDetector = mimeTypeDetector;
        PrincipalAccessor = principalAccessor;
    }

    public virtual Task<BlobManagementUploadTicketInfoDto> GetTicketAsync(string ticket)
    {
        var upload = ReadTicket(ticket, out var expiration);
        return Task.FromResult(new BlobManagementUploadTicketInfoDto
        {
            FileId = upload.FileId,
            MaxSize = upload.MaxSize,
            ExpirationTime = expiration.UtcDateTime
        });
    }

    [UnitOfWork(IsDisabled = true)]
    public virtual async Task<BlobManagementFileDto> UploadAsync(string ticket, IRemoteStreamContent file)
    {
        Check.NotNull(file, nameof(file));

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = file;
        var upload = ReadTicket(ticket, out _);

        using (PrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        using (CurrentTenant.Change(upload.TenantId))
        using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true))
        {
            await CheckUnusedAsync(upload);

            var name = !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim() : "file";
            CheckNameLength(name, nameof(IRemoteStreamContent.FileName));

            // Capped at the ticket's size whatever the request said its length was.
            await using var buffer = await BlobStreamBuffering.CopyToBufferAsync(content.GetStream(), upload.MaxSize, default);
            var contentType = await MimeTypeDetector.DetectAsync(buffer, name);
            if (!upload.Allows(contentType))
            {
                throw new BusinessException(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed)
                    .WithData("ContentType", contentType);
            }

            var stored = await FileDescriptorManager.UploadAsync(
                upload.ContainerName,
                name,
                buffer,
                upload.DirectoryId,
                descriptor =>
                {
                    descriptor.MarkPending();
                    descriptor.CreatorId = upload.UploaderId;
                },
                upload.ClientId,
                upload.FileId);

            await unitOfWork.CompleteAsync();
            return Map(stored);
        }
    }

    /// <summary>
    /// The ticket's content; <see cref="BlobManagementErrorCodes.UploadTicketInvalid"/> for one this Blob Management did
    /// not issue for uploads, one that was altered, or one that expired.
    /// </summary>
    protected virtual BlobManagementUploadTicket ReadTicket(string ticket, out DateTimeOffset expiration)
    {
        return TicketProtector.Unprotect<BlobManagementUploadTicket>(BlobManagementUploadTicket.Purpose, ticket, out expiration)
               ?? throw new BusinessException(BlobManagementErrorCodes.UploadTicketInvalid);
    }

    /// <summary>
    /// Refuses a ticket whose file is there already, or was and has been deleted: a ticket stores one file, once.
    /// </summary>
    protected virtual async Task CheckUnusedAsync(BlobManagementUploadTicket upload)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            if (await FileRepository.FindAsync(upload.FileId) != null)
            {
                throw new BusinessException(BlobManagementErrorCodes.UploadTicketUsed);
            }
        }
    }
}
