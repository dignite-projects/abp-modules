using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The directory service and the authorization of places - the top of a container, a directory, a restricted
/// directory - as signed-in users, with grants written through ABP's permission managers, against the database
/// provider of the concrete test class.
/// </summary>
public abstract class BlobManagementDirectories_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Container = BlobManagementTestContainers.Documents;

    private readonly IBlobManagementDirectoryAppService _directories;
    private readonly IBlobManagementFileAppService _files;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ResourcePermissionCheckCounter _counter;

    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    protected BlobManagementDirectories_Tests()
    {
        _directories = GetRequiredService<IBlobManagementDirectoryAppService>();
        _files = GetRequiredService<IBlobManagementFileAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _counter = GetRequiredService<ResourcePermissionCheckCounter>();
    }

    [Fact]
    public async Task Directories_Should_Be_Created_Listed_Renamed_Moved_And_Deleted()
    {
        await GrantAdminAsync();

        using (_principalAccessor.As(_admin))
        {
            var docs = await CreateAsync("Docs", null);
            var archive = await CreateAsync("Archive", null);
            docs.HasChildren.ShouldBeFalse();
            docs.IsRestricted.ShouldBeFalse();
            docs.CanUpdate.ShouldBeTrue();
            docs.Access.UploadFiles.ShouldBeTrue();

            var sub = await CreateAsync(" 2026 ", docs.Id);
            sub.Name.ShouldBe("2026");

            var top = await ListAsync(null);
            top.Select(d => d.Name).ShouldBe(new[] { "Docs", "Archive" });
            top.Single(d => d.Name == "Docs").HasChildren.ShouldBeTrue();

            (await _directories.UpdateAsync(sub.Id, new UpdateBlobManagementDirectoryInput { Name = "2027" })).Name.ShouldBe("2027");
            (await _directories.MoveAsync(sub.Id, new MoveBlobManagementDirectoryInput { ParentId = archive.Id, Order = 1 }))
                .ParentId.ShouldBe(archive.Id);
            (await ListAsync(archive.Id)).Single().Id.ShouldBe(sub.Id);

            var notEmpty = await Should.ThrowAsync<BusinessException>(() => _directories.DeleteAsync(archive.Id));
            notEmpty.Code.ShouldBe(BlobManagementErrorCodes.DirectoryNotEmpty);

            await _directories.DeleteAsync(sub.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _directories.GetAsync(sub.Id));
            // Deleting what is gone already is not an error.
            await _directories.DeleteAsync(sub.Id);
        }
    }

    [Fact]
    public async Task A_Container_Without_Directories_Should_Refuse_Them()
    {
        await GrantAdminAsync();

        using (_principalAccessor.As(_admin))
        {
            var docs = await CreateAsync("Docs", null);

            var listing = await Should.ThrowAsync<BusinessException>(() => _directories.GetListAsync(
                new GetBlobManagementDirectoriesInput { ContainerName = BlobManagementTestContainers.Files }));
            listing.Code.ShouldBe(BlobManagementErrorCodes.DirectoriesNotAllowed);

            var creating = await Should.ThrowAsync<BusinessException>(() => _directories.CreateAsync(
                new CreateBlobManagementDirectoryInput { ContainerName = BlobManagementTestContainers.Files, Name = "Folder" }));
            creating.Code.ShouldBe(BlobManagementErrorCodes.DirectoriesNotAllowed);

            // Its files are one flat list: a directory of another container is not found there.
            await Should.ThrowAsync<EntityNotFoundException>(() => UploadAsync(BlobManagementTestContainers.Files, docs.Id));
            (await UploadAsync(BlobManagementTestContainers.Files, null)).DirectoryId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task The_Directories_Of_A_Place_Should_Take_The_Permission_Of_The_Container()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto docs;
        using (_principalAccessor.As(_admin))
        {
            docs = await CreateAsync("Docs", null);
        }

        // Nothing without a grant: the top of a container lists what the caller may view, which is nothing.
        using (_principalAccessor.As(_alice))
        {
            (await ListAsync(null)).ShouldBeEmpty();
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.GetAsync(docs.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(docs.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", null));
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.CreateDirectories);

        using (_principalAccessor.As(_alice))
        {
            var listed = (await ListAsync(null)).Single();
            listed.Id.ShouldBe(docs.Id);
            listed.Access.View.ShouldBeTrue();
            listed.Access.CreateDirectories.ShouldBeTrue();
            listed.CanUpdate.ShouldBeFalse();
            listed.CanDelete.ShouldBeFalse();

            var mine = await CreateAsync("Mine", docs.Id);
            // Having created it gives nothing over it: renaming and deleting are the container's permissions.
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _directories.UpdateAsync(mine.Id, new UpdateBlobManagementDirectoryInput { Name = "Ours" }));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.DeleteAsync(mine.Id));
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.UpdateDirectories, BlobManagementPermissions.Container.DeleteDirectories);

        using (_principalAccessor.As(_alice))
        {
            var mine = (await ListAsync(docs.Id)).Single();
            mine.CanUpdate.ShouldBeTrue();
            (await _directories.UpdateAsync(mine.Id, new UpdateBlobManagementDirectoryInput { Name = "Ours" })).Name.ShouldBe("Ours");
            await _directories.DeleteAsync(mine.Id);
        }
    }

    [Fact]
    public async Task A_Restricted_Directory_Should_Be_Seen_And_Managed_Only_By_Those_Granted_On_It()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto departmentA;
        BlobManagementDirectoryDto departmentB;
        BlobManagementDirectoryDto open;
        BlobManagementDirectoryDto below;
        using (_principalAccessor.As(_admin))
        {
            departmentA = await CreateAsync("Department A", null);
            departmentB = await CreateAsync("Department B", null);
            open = await CreateAsync("Open", null);
            below = await CreateAsync("Plans", departmentA.Id);

            departmentA = await _directories.RestrictAsync(departmentA.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true });
            departmentA.IsRestricted.ShouldBeTrue();
            await _directories.RestrictAsync(departmentB.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true });
        }

        // Both may view the container and upload in it; each is granted on one department.
        foreach (var user in new[] { _alice, _bob })
        {
            await GrantOnContainerAsync(user, BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.UploadFiles);
        }

        await GrantOnDirectoryAsync(departmentA.Id, _alice,
            BlobManagementPermissions.Directory.View, BlobManagementPermissions.Directory.CreateDirectories);
        await GrantOnDirectoryAsync(departmentB.Id, _bob, BlobManagementPermissions.Directory.View);

        using (_principalAccessor.As(_alice))
        {
            // A restricted directory she is not granted on is not there for her.
            var top = await ListAsync(null);
            top.Select(d => d.Id).ShouldBe(new[] { departmentA.Id, open.Id });
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.GetAsync(departmentB.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(departmentB.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListFilesAsync(departmentB.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => UploadAsync(Container, departmentB.Id));

            // In hers, and everywhere below it, the directory's grants decide about directories...
            top.Single(d => d.Id == departmentA.Id).Access.CreateDirectories.ShouldBeTrue();
            var plans = (await ListAsync(departmentA.Id)).Single();
            plans.Id.ShouldBe(below.Id);
            plans.Access.CreateDirectories.ShouldBeTrue();
            (await CreateAsync("2026", below.Id)).ParentId.ShouldBe(below.Id);
            // ...and the container's about files.
            (await UploadAsync(Container, below.Id)).DirectoryId.ShouldBe(below.Id);

            // Outside it the container decides, which gave her no directory permission.
            top.Single(d => d.Id == open.Id).Access.CreateDirectories.ShouldBeFalse();
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", open.Id));

            // Saying which directories are restricted is managing permissions.
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _directories.RestrictAsync(departmentA.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = false }));
        }

        using (_principalAccessor.As(_bob))
        {
            (await ListAsync(null)).Select(d => d.Id).ShouldBe(new[] { departmentB.Id, open.Id });
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListFilesAsync(below.Id));
            // He views his department, and was given nothing to do with its directories.
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", departmentB.Id));
        }

        // The management permission sees every directory.
        using (_principalAccessor.As(_admin))
        {
            (await ListAsync(null)).Select(d => d.Id).ShouldBe(new[] { departmentA.Id, departmentB.Id, open.Id });
            await _directories.RestrictAsync(departmentB.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = false });
        }

        // No longer restricted, it follows the container again.
        using (_principalAccessor.As(_alice))
        {
            (await ListAsync(null)).Select(d => d.Id).ShouldContain(departmentB.Id);
        }
    }

    [Fact]
    public async Task A_Directory_Granted_On_Should_Be_Reached_Without_A_Grant_On_The_Container()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto granted;
        using (_principalAccessor.As(_admin))
        {
            granted = await CreateAsync("Granted", null);
            await CreateAsync("Other", null);
            await _directories.RestrictAsync(granted.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true });
        }

        await GrantOnDirectoryAsync(granted.Id, _alice, BlobManagementPermissions.Directory.View);

        using (_principalAccessor.As(_alice))
        {
            (await ListAsync(null)).Single().Id.ShouldBe(granted.Id);
            (await ListFilesAsync(granted.Id)).TotalCount.ShouldBe(0);

            // The top of the container is not hers to view, and files are the container's permission.
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListFilesAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => UploadAsync(Container, granted.Id));
        }
    }

    [Fact]
    public async Task A_Move_Should_Take_The_Place_It_Leaves_And_The_Place_It_Lands()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto folder;
        BlobManagementDirectoryDto restricted;
        using (_principalAccessor.As(_admin))
        {
            folder = await CreateAsync("Folder", null);
            restricted = await CreateAsync("Restricted", null);
            await _directories.RestrictAsync(restricted.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true });
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.UpdateDirectories);
        await GrantOnDirectoryAsync(restricted.Id, _alice, BlobManagementPermissions.Directory.View);

        using (_principalAccessor.As(_alice))
        {
            // She may move directories at the top, but not create one where it would land.
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _directories.MoveAsync(folder.Id, new MoveBlobManagementDirectoryInput { ParentId = restricted.Id }));
            // Among its siblings it takes no more than moving.
            (await _directories.MoveAsync(folder.Id, new MoveBlobManagementDirectoryInput { ParentId = null, Order = 0 }))
                .ParentId.ShouldBeNull();
        }

        await GrantOnDirectoryAsync(restricted.Id, _alice, BlobManagementPermissions.Directory.CreateDirectories);

        using (_principalAccessor.As(_alice))
        {
            var moved = await _directories.MoveAsync(folder.Id, new MoveBlobManagementDirectoryInput { ParentId = restricted.Id });
            moved.ParentId.ShouldBe(restricted.Id);
            // Where it is now, the restricted directory decides: she may not move it again.
            moved.CanUpdate.ShouldBeFalse();
        }

        // A restricted directory keeps its restriction where it goes.
        using (_principalAccessor.As(_admin))
        {
            var target = await CreateAsync("Target", null);
            (await _directories.MoveAsync(restricted.Id, new MoveBlobManagementDirectoryInput { ParentId = target.Id }))
                .IsRestricted.ShouldBeTrue();
        }

        using (_principalAccessor.As(_bob))
        {
            await GrantOnContainerAsync(_bob, BlobManagementPermissions.Container.View);
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.GetAsync(folder.Id));
        }
    }

    [Fact]
    public async Task A_Listing_Should_Ask_The_Grant_Store_For_The_Place_Not_For_Each_Item()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto folder;
        using (_principalAccessor.As(_admin))
        {
            folder = await CreateAsync("Folder", null);
            for (var i = 0; i < 5; i++)
            {
                await CreateAsync("Directory " + i, folder.Id);
                await UploadAsync(Container, folder.Id);
            }

            var restricted = (await ListAsync(folder.Id)).Take(2).ToList();
            foreach (var directory in restricted)
            {
                await _directories.RestrictAsync(directory.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true });
            }
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View);

        using (_principalAccessor.As(_alice))
        {
            // Five files: one question, about the container.
            _counter.Reset();
            (await ListFilesAsync(folder.Id)).TotalCount.ShouldBe(5);
            _counter.Count.ShouldBe(1);

            // Five directories, two of them restricted: the container, and the two that have to be told apart.
            _counter.Reset();
            (await ListAsync(folder.Id)).Count.ShouldBe(3);
            _counter.Count.ShouldBe(3);
        }
    }

    [Fact]
    public async Task Deleting_A_Directory_Should_Delete_Its_Grants()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto folder;
        using (_principalAccessor.As(_admin))
        {
            folder = await CreateAsync("Folder", null);
        }

        await GrantOnDirectoryAsync(folder.Id, _alice, BlobManagementPermissions.Directory.View);
        (await GetGrantsAsync(folder.Id)).ShouldNotBeEmpty();

        using (_principalAccessor.As(_admin))
        {
            await _directories.DeleteAsync(folder.Id);
        }

        (await GetGrantsAsync(folder.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Directory_Service_Should_Serve_Users_And_Be_Closed_When_The_Tenants_Blob_Management_Is_Off()
    {
        var client = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(AbpClaimTypes.ClientId, "site-app") }, "Test"));
        using (_principalAccessor.Change(client))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(null));
        }

        var off = Guid.NewGuid();
        GetRequiredService<FakeFeatureStore>().SetForTenant(off, BlobManagementFeatures.Enable, "false");
        using (GetRequiredService<ICurrentTenant>().Change(off))
        using (_principalAccessor.As(_alice))
        {
            var refused = await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(null));
            refused.Code.ShouldBe(AbpFeatureErrorCodes.AtLeastOneOfTheseFeaturesMustBeEnabled);
        }
    }

    private Task<BlobManagementDirectoryDto> CreateAsync(string name, Guid? parentId)
    {
        return _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = name, ParentId = parentId });
    }

    private async Task<System.Collections.Generic.IReadOnlyList<BlobManagementDirectoryDto>> ListAsync(Guid? parentId)
    {
        return (await _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container, ParentId = parentId })).Items;
    }

    private Task<Volo.Abp.Application.Dtos.PagedResultDto<BlobManagementFileDto>> ListFilesAsync(Guid? directoryId)
    {
        return _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, DirectoryId = directoryId });
    }

    private Task<BlobManagementFileDto> UploadAsync(string containerName, Guid? directoryId)
    {
        return _files.CreateAsync(new CreateBlobManagementFileInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            File = new RemoteStreamContent(
                new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), Guid.NewGuid().ToString("N") + ".pdf", "application/pdf")
        });
    }

    /// <summary>The management permission with all of its children, and managing permissions.</summary>
    private Task GrantAdminAsync()
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().GrantAsync(_admin, BlobManagementPermissions.GetAll()));
    }

    private Task GrantOnContainerAsync(Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionManager>().GrantOnContainerAsync(Container, userId, permissions));
    }

    private Task GrantOnDirectoryAsync(Guid directoryId, Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionManager>().GrantOnDirectoryAsync(directoryId, userId, permissions));
    }

    private Task<System.Collections.Generic.List<ResourcePermissionGrant>> GetGrantsAsync(Guid directoryId)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionGrantRepository>()
            .GetPermissionsAsync(BlobManagementResourceNames.Directory, directoryId.ToString()));
    }
}
