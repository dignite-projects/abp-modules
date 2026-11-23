using System;
using System.Linq;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Tickets;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// Issues upload tickets for the file services, which authorize the caller and check the place first - beside
/// <c>BlobManagementReadLinkIssuer</c>, which issues read links. A ticket names the tenant, the container, the place,
/// the id its file takes, the user and the client, the size and the types, and is signed with
/// <see cref="BlobManagementTicketProtector"/>.
/// </summary>
public class BlobManagementUploadTicketIssuer : ITransientDependency
{
    protected BlobManagementTicketProtector TicketProtector { get; }

    protected IBlobContainerConfigurationProvider ContainerConfigurationProvider { get; }

    protected BlobManagementUploadOptions Options { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected ICurrentUser CurrentUser { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    public BlobManagementUploadTicketIssuer(
        BlobManagementTicketProtector ticketProtector,
        IBlobContainerConfigurationProvider containerConfigurationProvider,
        IOptions<BlobManagementUploadOptions> options,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        FileDescriptorManager fileDescriptorManager)
    {
        TicketProtector = ticketProtector;
        ContainerConfigurationProvider = containerConfigurationProvider;
        Options = options.Value;
        GuidGenerator = guidGenerator;
        CurrentTenant = currentTenant;
        CurrentUser = currentUser;
        FileDescriptorManager = fileDescriptorManager;
    }

    /// <summary>
    /// The ticket for one file in the place <paramref name="input"/> names, in the current tenant, recording
    /// <paramref name="clientId"/> as the writer of the file it brings in when a remote client asked for it. Its size
    /// is the input's, never more than the container takes; its lifetime the input's, never more than
    /// <see cref="BlobManagementUploadOptions.MaxTicketLifetime"/>; its user the input's, else the current one. A box
    /// for its pictures is checked now, as the upload will apply it (<see cref="FileDescriptorManager.GetSaveConfiguration"/>):
    /// one the container cannot take is refused before a ticket is signed, not when the browser brings the file.
    /// </summary>
    public virtual BlobManagementUploadTicketDto Issue(CreateBlobManagementUploadTicketInput input, string? clientId)
    {
        FileDescriptorManager.GetSaveConfiguration(input.ContainerName, input.ImageMaxWidth, input.ImageMaxHeight);

        var containerMax = BlobStreamBuffering.GetMaxBufferedBytes(ContainerConfigurationProvider.Get(input.ContainerName));
        var lifetime = input.LifetimeInSeconds is > 0
            ? TimeSpan.FromSeconds(input.LifetimeInSeconds.Value)
            : Options.TicketLifetime;
        if (lifetime > Options.MaxTicketLifetime)
        {
            lifetime = Options.MaxTicketLifetime;
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
                .ToList(),
            ImageMaxWidth = input.ImageMaxWidth,
            ImageMaxHeight = input.ImageMaxHeight
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
}
