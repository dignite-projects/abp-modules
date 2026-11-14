using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Links;
using Dignite.Abp.BlobManagement.Tickets;
using Dignite.Abp.BlobManagement.Uploads;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The in-process <see cref="IBlobManagementFileService"/>: an application inside Blob Management's own host calls it,
/// a trusted caller, so it checks no permission and no client - it goes straight to <see cref="FileDescriptorManager"/>,
/// whose are storing, dedup and the quota. Never exposed over HTTP (<c>RemoteService(IsEnabled = false)</c>, so
/// conventional controllers skip it); the file API authorizes its remote callers before it reaches the storage. Part of
/// the tenant's Blob Management: with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[RemoteService(IsEnabled = false)]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementFileService : BlobManagementAppService, IBlobManagementFileService
{
    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    // Resolved lazily, not injected, so the constructor - which the file API repeats - stays as it was.
    protected BlobManagementTicketProtector TicketProtector =>
        LazyServiceProvider.LazyGetRequiredService<BlobManagementTicketProtector>();

    protected BlobManagementReadLinkIssuer ReadLinkIssuer =>
        LazyServiceProvider.LazyGetRequiredService<BlobManagementReadLinkIssuer>();

    protected BlobManagementUploadOptions UploadOptions =>
        LazyServiceProvider.LazyGetRequiredService<IOptions<BlobManagementUploadOptions>>().Value;

    public BlobManagementFileService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
    {
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return Map(await GetFileAsync(id));
    }

    public virtual async Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        GetContainer(input.ContainerName);
        if (input.DirectoryId.HasValue)
        {
            await GetDirectoryAsync(input.DirectoryId.Value, input.ContainerName);
        }

        var count = await FileRepository.GetCountAsync(input.ContainerName, input.DirectoryId, input.Filter);
        var files = await FileRepository.GetListAsync(
            input.ContainerName,
            input.DirectoryId,
            input.Filter,
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount);

        return new PagedResultDto<BlobManagementFileDto>(count, files.Select(Map).ToList());
    }

    public virtual Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        return CreateAsync(input, clientId: null);
    }

    public virtual async Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        var file = await GetFileAsync(id);
        await FileDescriptorManager.RenameAsync(file, input.Name.Trim());
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        var file = await GetFileAsync(id);
        await FileDescriptorManager.MoveAsync(file, input.DirectoryId);
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input)
    {
        var file = await GetFileAsync(id);
        await FileDescriptorManager.SetPublicAsync(file, input.IsPublic);
        return Map(file);
    }

    public virtual Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input)
    {
        return CreateUploadTicketAsync(input, clientId: null);
    }

    public virtual async Task<BlobManagementFileDto> ConfirmAsync(Guid id)
    {
        var file = await GetFileAsync(id);
        await FileDescriptorManager.ConfirmAsync(file);
        return Map(file);
    }

    public virtual async Task<BlobManagementReadLinkDto> CreateReadLinkAsync(Guid id, CreateBlobManagementReadLinkInput input)
    {
        return await ReadLinkIssuer.IssueAsync(await GetFileAsync(id), input?.LifetimeInSeconds);
    }

    public virtual async Task<ListResultDto<BlobManagementReadLinkDto>> CreateReadLinksAsync(CreateBlobManagementReadLinksInput input)
    {
        var links = new List<BlobManagementReadLinkDto>();
        foreach (var id in input.Ids.Distinct())
        {
            var file = await FindFileForLinkAsync(id);
            if (file is { IsPending: false })
            {
                links.Add(await ReadLinkIssuer.IssueAsync(file, input.LifetimeInSeconds));
            }
        }

        return new ListResultDto<BlobManagementReadLinkDto>(links);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var file = await FindFileAsync(id);
        if (file != null)
        {
            await FileDescriptorManager.DeleteAsync(file);
        }
    }

    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        var file = await GetFileAsync(id);
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
        return new RemoteStreamContent(stream, file.Name, file.MimeType, file.Size);
    }

    /// <summary>
    /// Stores the file, recording <paramref name="clientId"/> as its writer when a remote client sent it. The user it
    /// is counted for is the one the caller named; without one, ABP's auditing records the current user.
    /// </summary>
    protected virtual async Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input, string? clientId)
    {
        Check.NotNull(input.File, nameof(input.File));

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = input.File;
        var name = !input.Name.IsNullOrWhiteSpace() ? input.Name!.Trim()
            : !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim()
            : "file";

        // The input's own name was validated with the input; the uploaded file's was not.
        CheckNameLength(name, nameof(input.Name));

        var creatorId = input.CreatorId;
        var isPublic = input.IsPublic;
        var file = await FileDescriptorManager.UploadAsync(
            input.ContainerName,
            name,
            content.GetStream(),
            input.DirectoryId,
            descriptor =>
            {
                descriptor.SetPublic(isPublic);
                if (creatorId.HasValue)
                {
                    descriptor.CreatorId = creatorId;
                }
            },
            clientId);

        return Map(file);
    }

    /// <summary>
    /// Issues an upload ticket, recording <paramref name="clientId"/> as the writer of the file it brings in when a
    /// remote client asked for it. The place is checked now - a container that is not managed, or a directory that is
    /// not one of the container, is refused before a ticket exists. The ticket's size is the input's, never more than
    /// the container takes; its lifetime the input's, never more than
    /// <see cref="BlobManagementUploadOptions.MaxTicketLifetime"/>; its user the input's, else the current one.
    /// </summary>
    protected virtual async Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(
        CreateBlobManagementUploadTicketInput input,
        string? clientId)
    {
        GetContainer(input.ContainerName);
        if (input.DirectoryId.HasValue)
        {
            await GetDirectoryAsync(input.DirectoryId.Value, input.ContainerName);
        }

        var containerMax = BlobStreamBuffering.GetMaxBufferedBytes(ContainerConfigurationProvider.Get(input.ContainerName));
        var lifetime = input.LifetimeInSeconds is > 0
            ? TimeSpan.FromSeconds(input.LifetimeInSeconds.Value)
            : UploadOptions.TicketLifetime;
        if (lifetime > UploadOptions.MaxTicketLifetime)
        {
            lifetime = UploadOptions.MaxTicketLifetime;
        }

        var upload = new BlobManagementUploadTicket
        {
            TenantId = CurrentTenant.Id,
            ContainerName = input.ContainerName,
            DirectoryId = input.DirectoryId,
            FileId = GuidGenerator.Create(),
            UploaderId = input.UploaderId ?? CurrentUser.Id,
            ClientId = clientId,
            MaxSize = input.MaxSize is > 0 ? Math.Min(input.MaxSize.Value, containerMax) : containerMax,
            AllowedContentTypes = (input.AllowedContentTypes ?? new())
                .Where(type => !type.IsNullOrWhiteSpace())
                .Select(type => type.Trim().ToLowerInvariant())
                .Distinct()
                .ToList()
        };

        var expiration = DateTimeOffset.UtcNow.Add(lifetime);
        var ticket = TicketProtector.Protect(BlobManagementUploadTicket.Purpose, upload, expiration);

        return new BlobManagementUploadTicketDto
        {
            Ticket = ticket,
            UploadUrl = BlobManagementUploadUrl.Build(ticket),
            FileId = upload.FileId,
            ExpirationTime = expiration.UtcDateTime
        };
    }

    /// <summary>A file the caller may have a read link for, or <c>null</c>: in process, any file of a managed container.</summary>
    protected virtual Task<FileDescriptor?> FindFileForLinkAsync(Guid id)
    {
        return FindFileAsync(id);
    }

    /// <summary>A file of a managed container; any other is not found.</summary>
    protected virtual async Task<FileDescriptor> GetFileAsync(Guid id)
    {
        return await FindFileAsync(id) ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
    }

    /// <summary>A file of a managed container, or <c>null</c>.</summary>
    protected virtual async Task<FileDescriptor?> FindFileAsync(Guid id)
    {
        var file = await FileRepository.FindAsync(id);
        return file != null && IsManagedContainer(file.ContainerName) ? file : null;
    }
}
