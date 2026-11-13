using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Abp.BlobManagement.Tickets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.Content;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A user's ticket for adding a file to a container: issued as the upload would be authorized, redeemed by anyone who
/// holds it, as the user who issued it - whose permissions decide again at that moment.
/// </summary>
public abstract class BlobManagementFileUploadTicket_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBlobManagementFileAppService _files;
    private readonly IBlobManagementTicketRedemptionAppService _redemption;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly string _editors = "editors-" + Guid.NewGuid().ToString("N");

    protected BlobManagementFileUploadTicket_Tests()
    {
        _files = GetRequiredService<IBlobManagementFileAppService>();
        _redemption = GetRequiredService<IBlobManagementTicketRedemptionAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task A_Ticket_Should_Add_One_File_As_Its_Issuer()
    {
        await GrantAsync(BlobManagementPermissions.Containers.Default, UserPermissionValueProvider.ProviderName, _alice.ToString());
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _alice.ToString());

        BlobManagementTicketDto ticket;
        using (As(_alice))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                FileName = " notes.pdf "
            });
        }

        ticket.Kind.ShouldBe(BlobManagementTicketKind.FileUpload);
        ticket.Url.ShouldStartWith(BlobManagementTicketUrl.FileUploadPath + "?ticket=");

        // Redeemed by whoever holds it: no one is signed in here.
        var file = await _redemption.UploadFileAsync(ticket.Ticket, Content("whatever-the-sender-calls-it.bin"));
        file.Name.ShouldBe("notes.pdf");
        file.CreatorId.ShouldBe(_alice);
        file.MimeType.ShouldBe("application/pdf");

        var again = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
        again.Code.ShouldBe(BlobManagementErrorCodes.InvalidTicket);
    }

    [Fact]
    public async Task A_Ticket_Should_Not_Be_Issued_For_An_Upload_That_Would_Be_Refused()
    {
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                FileName = "nope.pdf"
            }));
        }
    }

    [Fact]
    public async Task The_Issuers_Permissions_Should_Decide_When_It_Is_Used()
    {
        await GrantAsync(BlobManagementPermissions.Containers.Default, UserPermissionValueProvider.ProviderName, _bob.ToString());
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _bob.ToString());

        BlobManagementTicketDto ticket;
        using (As(_bob))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                FileName = "from-bob.pdf"
            });
        }

        // Revoked after the ticket was issued: refused, and not spent.
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _bob.ToString(), false);
        var refused = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
        refused.Code.ShouldBe(BlobManagementErrorCodes.InvalidTicket);

        await GrantAsync(BlobManagementPermissions.Containers.Default, UserPermissionValueProvider.ProviderName, _bob.ToString());
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _bob.ToString());
        var file = await _redemption.UploadFileAsync(ticket.Ticket, Content());
        file.ContainerName.ShouldBe(BlobManagementTestContainers.Documents);
        file.CreatorId.ShouldBe(_bob);
    }

    [Fact]
    public async Task The_Issuers_Roles_Should_Travel_With_The_Ticket()
    {
        await GrantAsync(BlobManagementPermissions.Containers.Default, RolePermissionValueProvider.ProviderName, _editors);
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, RolePermissionValueProvider.ProviderName, _editors);

        BlobManagementTicketDto ticket;
        using (As(_bob, _editors))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                FileName = "team.pdf"
            });
        }

        (await _redemption.UploadFileAsync(ticket.Ticket, Content())).CreatorId.ShouldBe(_bob);
    }

    [Fact]
    public async Task A_File_Upload_Ticket_Should_Not_Be_Redeemed_As_Another_Kind()
    {
        await GrantAsync(BlobManagementPermissions.Containers.Default, UserPermissionValueProvider.ProviderName, _alice.ToString());
        await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _alice.ToString());
        BlobManagementTicketDto ticket;
        using (As(_alice))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                FileName = "kind.pdf"
            });
        }

        var asSystemUpload = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadAsync(ticket.Ticket, Content()));
        asSystemUpload.Code.ShouldBe(BlobManagementErrorCodes.InvalidTicket);
        var asDownload = await Should.ThrowAsync<BusinessException>(() => _redemption.DownloadAsync(ticket.Ticket));
        asDownload.Code.ShouldBe(BlobManagementErrorCodes.InvalidTicket);

        // Still good for its own kind.
        (await _redemption.UploadFileAsync(ticket.Ticket, Content())).Name.ShouldBe("kind.pdf");
    }

    [Fact]
    public async Task A_File_Upload_Ticket_Should_Answer_BlobManagementDisabled_When_The_Tenants_BlobManagement_Is_Turned_Off()
    {
        var tenant = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(tenant))
        {
            await GrantAsync(BlobManagementPermissions.Containers.Default, UserPermissionValueProvider.ProviderName, _alice.ToString());
            await GrantAsync(BlobManagementPermissions.Containers.UploadFiles, UserPermissionValueProvider.ProviderName, _alice.ToString());

            // The ticket carries its tenant: issued while Blob Management is on (the feature's default).
            BlobManagementTicketDto ticket;
            using (As(_alice))
            {
                ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
                {
                    ContainerName = BlobManagementTestContainers.Documents,
                    FileName = "late.pdf"
                });
            }

            GetRequiredService<FakeFeatureStore>().SetForTenant(tenant, BlobManagementFeatures.Enable, "false");

            var refused = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
            refused.Code.ShouldBe(BlobManagementErrorCodes.BlobManagementDisabled);
        }
    }

    private static IRemoteStreamContent Content(string fileName = "upload.pdf")
    {
        return new RemoteStreamContent(new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), fileName, "application/octet-stream");
    }

    private IDisposable As(Guid userId, params string[] roles)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }

    private Task GrantAsync(string permission, string providerName, string providerKey, bool isGranted = true)
    {
        return WithUnitOfWorkAsync(() =>
            GetRequiredService<IPermissionManager>().SetAsync(permission, providerName, providerKey, isGranted));
    }
}
