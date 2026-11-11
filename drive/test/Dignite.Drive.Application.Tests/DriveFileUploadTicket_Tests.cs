using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Drive.Features;
using Dignite.Drive.Files;
using Dignite.Drive.Permissions;
using Dignite.Drive.Tickets;
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

namespace Dignite.Drive;

/// <summary>
/// A user's ticket for adding a file to a user drive: issued as the upload would be authorized, redeemed by anyone who
/// holds it, as the user who issued it - whose permissions decide again at that moment.
/// </summary>
public abstract class DriveFileUploadTicket_Tests<TStartupModule> : DriveApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDriveFileAppService _files;
    private readonly IDriveTicketRedemptionAppService _redemption;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly string _editors = "editors-" + Guid.NewGuid().ToString("N");

    protected DriveFileUploadTicket_Tests()
    {
        _files = GetRequiredService<IDriveFileAppService>();
        _redemption = GetRequiredService<IDriveTicketRedemptionAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task A_Ticket_Should_Add_One_File_As_Its_Issuer()
    {
        await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _alice.ToString());

        DriveTicketDto ticket;
        using (As(_alice))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveTestContainers.UserDrive,
                FileName = " notes.pdf "
            });
        }

        ticket.Kind.ShouldBe(DriveTicketKind.FileUpload);
        ticket.Url.ShouldStartWith(DriveTicketUrl.FileUploadPath + "?ticket=");

        // Redeemed by whoever holds it: no one is signed in here.
        var file = await _redemption.UploadFileAsync(ticket.Ticket, Content("whatever-the-sender-calls-it.bin"));
        file.Name.ShouldBe("notes.pdf");
        file.CreatorId.ShouldBe(_alice);
        file.MimeType.ShouldBe("application/pdf");

        var again = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
        again.Code.ShouldBe(DriveErrorCodes.InvalidTicket);
    }

    [Fact]
    public async Task A_Ticket_Should_Not_Be_Issued_For_An_Upload_That_Would_Be_Refused()
    {
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveTestContainers.UserDrive,
                FileName = "nope.pdf"
            }));
        }
    }

    [Fact]
    public async Task The_Issuers_Permissions_Should_Decide_When_It_Is_Used()
    {
        await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _bob.ToString());

        DriveTicketDto ticket;
        using (As(_bob))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveTestContainers.UserDrive,
                FileName = "from-bob.pdf"
            });
        }

        // Revoked after the ticket was issued: refused, and not spent.
        await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _bob.ToString(), false);
        var refused = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
        refused.Code.ShouldBe(DriveErrorCodes.InvalidTicket);

        await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _bob.ToString());
        var file = await _redemption.UploadFileAsync(ticket.Ticket, Content());
        file.ContainerName.ShouldBe(DriveTestContainers.UserDrive);
        file.CreatorId.ShouldBe(_bob);
    }

    [Fact]
    public async Task The_Issuers_Roles_Should_Travel_With_The_Ticket()
    {
        await GrantAsync(DrivePermissions.Files.Upload, RolePermissionValueProvider.ProviderName, _editors);

        DriveTicketDto ticket;
        using (As(_bob, _editors))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveTestContainers.UserDrive,
                FileName = "team.pdf"
            });
        }

        (await _redemption.UploadFileAsync(ticket.Ticket, Content())).CreatorId.ShouldBe(_bob);
    }

    [Fact]
    public async Task A_File_Upload_Ticket_Should_Not_Be_Redeemed_As_Another_Kind()
    {
        await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _alice.ToString());
        DriveTicketDto ticket;
        using (As(_alice))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveTestContainers.UserDrive,
                FileName = "kind.pdf"
            });
        }

        var asSystemUpload = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadAsync(ticket.Ticket, Content()));
        asSystemUpload.Code.ShouldBe(DriveErrorCodes.InvalidTicket);
        var asDownload = await Should.ThrowAsync<BusinessException>(() => _redemption.DownloadAsync(ticket.Ticket));
        asDownload.Code.ShouldBe(DriveErrorCodes.InvalidTicket);

        // Still good for its own kind.
        (await _redemption.UploadFileAsync(ticket.Ticket, Content())).Name.ShouldBe("kind.pdf");
    }

    [Fact]
    public async Task A_File_Upload_Ticket_Should_Answer_DriveDisabled_When_The_Tenants_Drive_Is_Turned_Off()
    {
        var tenant = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(tenant))
        {
            await GrantAsync(DrivePermissions.Files.Upload, UserPermissionValueProvider.ProviderName, _alice.ToString());

            // The ticket carries its tenant: issued while the Drive is on (the feature's default).
            DriveTicketDto ticket;
            using (As(_alice))
            {
                ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
                {
                    ContainerName = DriveTestContainers.UserDrive,
                    FileName = "late.pdf"
                });
            }

            GetRequiredService<FakeFeatureStore>().SetForTenant(tenant, DriveFeatures.Enable, "false");

            var refused = await Should.ThrowAsync<BusinessException>(() => _redemption.UploadFileAsync(ticket.Ticket, Content()));
            refused.Code.ShouldBe(DriveErrorCodes.DriveDisabled);
        }
    }

    private static IRemoteStreamContent Content(string fileName = "upload.pdf")
    {
        return new RemoteStreamContent(new MemoryStream(DriveTestFiles.Pdf(Guid.NewGuid().ToString())), fileName, "application/octet-stream");
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
