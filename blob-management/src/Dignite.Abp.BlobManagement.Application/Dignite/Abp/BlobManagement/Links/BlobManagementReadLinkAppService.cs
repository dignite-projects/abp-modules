using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Public;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// The anonymous read with a link. The link is the whole authorization - the tenant and the file - and the request
/// brings nothing else that is used: the call runs under an anonymous principal, in the link's tenant. The file is
/// answered from one lookup, as a public read is: its entity tag is its physical BLOB, and a matching
/// <c>If-None-Match</c> is answered without reading the bytes. Not gated by <c>BlobManagement.Enable</c>, as public
/// reads are not.
/// Exposed only through <c>BlobManagementReadLinkController</c>.
/// </summary>
[AllowAnonymous]
[RemoteService(IsEnabled = false)]
public class BlobManagementReadLinkAppService : BlobManagementAppService, IBlobManagementReadLinkAppService
{
    protected BlobManagementTicketProtector TicketProtector { get; }

    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected ICurrentPrincipalAccessor PrincipalAccessor { get; }

    public BlobManagementReadLinkAppService(
        BlobManagementTicketProtector ticketProtector,
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager,
        ICurrentPrincipalAccessor principalAccessor)
    {
        TicketProtector = ticketProtector;
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
        PrincipalAccessor = principalAccessor;
    }

    [UnitOfWork(IsDisabled = true)]
    public virtual async Task<BlobManagementReadLinkResult> ReadAsync(string link, BlobManagementPublicReadRequest request)
    {
        var read = TicketProtector.Unprotect<BlobManagementReadLink>(BlobManagementReadLink.Purpose, link, out var expiration)
                   ?? throw new BusinessException(BlobManagementErrorCodes.ReadLinkInvalid);

        using (PrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        using (CurrentTenant.Change(read.TenantId))
        using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var file = await FileRepository.FindAsync(read.FileId);
            if (file == null || file.IsPending || !IsManagedContainer(file.ContainerName))
            {
                throw new EntityNotFoundException(typeof(FileDescriptor), read.FileId);
            }

            var result = new BlobManagementReadLinkResult
            {
                ExpirationTime = expiration.UtcDateTime,
                File = new BlobManagementPublicFileDto
                {
                    Name = file.Name,
                    MimeType = file.MimeType,
                    Size = file.Size,
                    ContentId = file.BlobName,
                    EntityTag = file.BlobName.Replace("\"", string.Empty).Replace("\\", string.Empty)
                }
            };

            if (request != null &&
                (request.IfNoneMatchAny ||
                 request.IfNoneMatch.Any(tag => string.Equals(tag, result.File.EntityTag, StringComparison.Ordinal))))
            {
                result.NotModified = true;
            }
            else if (request?.HeadersOnly != true)
            {
                var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                             ?? throw new EntityNotFoundException(typeof(FileDescriptor), file.Id);
                result.Content = new RemoteStreamContent(stream, file.Name, file.MimeType, file.Size);
            }

            await unitOfWork.CompleteAsync();
            return result;
        }
    }
}
