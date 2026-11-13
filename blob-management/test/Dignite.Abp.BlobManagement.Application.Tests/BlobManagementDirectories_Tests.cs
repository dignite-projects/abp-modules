using System;
using System.Collections.Generic;
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
/// The directory service - the one thing a user changes in Blob Management - as signed-in users, with grants written
/// through ABP's permission managers, against the database provider of the concrete test class.
/// </summary>
public abstract class BlobManagementDirectories_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Container = BlobManagementTestContainers.Documents;

    private readonly IBlobManagementDirectoryAppService _directories;
    private readonly IBlobManagementFileService _files;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ResourcePermissionCheckCounter _counter;

    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();

    protected BlobManagementDirectories_Tests()
    {
        _directories = GetRequiredService<IBlobManagementDirectoryAppService>();
        _files = GetRequiredService<IBlobManagementFileService>();
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

            // Its files are one flat list: an application files nothing under a directory there.
            var filing = await Should.ThrowAsync<BusinessException>(() => StoreAsync(BlobManagementTestContainers.Files, docs.Id));
            filing.Code.ShouldBe(BlobManagementErrorCodes.DirectoriesNotAllowed);
            (await StoreAsync(BlobManagementTestContainers.Files, null)).DirectoryId.ShouldBeNull();

            // A directory of another container is not found: as a parent, or as where a moved directory lands.
            var elsewhere = await _directories.CreateAsync(
                new CreateBlobManagementDirectoryInput { ContainerName = BlobManagementTestContainers.Archive, Name = "Elsewhere" });
            await Should.ThrowAsync<EntityNotFoundException>(() => CreateAsync("Folder", elsewhere.Id));
            await Should.ThrowAsync<EntityNotFoundException>(() => ListAsync(elsewhere.Id));
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _directories.MoveAsync(docs.Id, new MoveBlobManagementDirectoryInput { ParentId = elsewhere.Id }));
        }
    }

    [Fact]
    public async Task The_Directories_Of_A_Container_Should_Take_Its_Permissions()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto docs;
        BlobManagementDirectoryDto deep;
        using (_principalAccessor.As(_admin))
        {
            docs = await CreateAsync("Docs", null);
            deep = await CreateAsync("Deep", docs.Id);
        }

        // Nothing without a grant - not even the tree.
        using (_principalAccessor.As(_alice))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.GetAsync(docs.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", null));
        }

        // Creating alone is not enough: the caller has to see the tree.
        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.CreateDirectories);
        using (_principalAccessor.As(_alice))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", null));
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View);
        using (_principalAccessor.As(_alice))
        {
            (await ListAsync(null)).Single().Id.ShouldBe(docs.Id);
            (await _directories.GetAsync(deep.Id)).Name.ShouldBe("Deep");

            // The container's answer holds at every depth.
            var mine = await CreateAsync("Mine", deep.Id);

            // Having created it gives nothing over it: renaming, moving and deleting are permissions of their own.
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _directories.UpdateAsync(mine.Id, new UpdateBlobManagementDirectoryInput { Name = "Ours" }));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _directories.MoveAsync(mine.Id, new MoveBlobManagementDirectoryInput { ParentId = null }));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.DeleteAsync(mine.Id));

            // The grant is on that container only.
            await Should.ThrowAsync<AbpAuthorizationException>(() => _directories.CreateAsync(
                new CreateBlobManagementDirectoryInput { ContainerName = BlobManagementTestContainers.Archive, Name = "Mine" }));
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.UpdateDirectories, BlobManagementPermissions.Container.DeleteDirectories);
        using (_principalAccessor.As(_alice))
        {
            var mine = (await ListAsync(deep.Id)).Single();
            (await _directories.UpdateAsync(mine.Id, new UpdateBlobManagementDirectoryInput { Name = "Ours" })).Name.ShouldBe("Ours");
            (await _directories.MoveAsync(mine.Id, new MoveBlobManagementDirectoryInput { ParentId = null })).ParentId.ShouldBeNull();
            await _directories.DeleteAsync(mine.Id);
        }
    }

    [Fact]
    public async Task A_Module_Wide_Directory_Permission_Should_Count_In_A_Container_The_Caller_Sees()
    {
        await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>()
            .GrantAsync(_alice, BlobManagementPermissions.Containers.CreateDirectories));

        // Module-wide, but still only where the caller sees the tree.
        using (_principalAccessor.As(_alice))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Mine", null));
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View);
        using (_principalAccessor.As(_alice))
        {
            (await CreateAsync("Mine", null)).Name.ShouldBe("Mine");
        }
    }

    [Fact]
    public async Task A_Listing_Should_Ask_The_Grant_Store_For_The_Container_Not_For_Each_Directory()
    {
        await GrantAdminAsync();
        BlobManagementDirectoryDto folder;
        using (_principalAccessor.As(_admin))
        {
            folder = await CreateAsync("Folder", null);
            for (var i = 0; i < 5; i++)
            {
                await CreateAsync("Directory " + i, folder.Id);
            }
        }

        await GrantOnContainerAsync(_alice, BlobManagementPermissions.Container.View);

        using (_principalAccessor.As(_alice))
        {
            _counter.Reset();
            (await ListAsync(folder.Id)).Count.ShouldBe(5);
            _counter.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task The_Directory_Service_Should_Serve_Users_And_Be_Closed_When_The_Tenants_Blob_Management_Is_Off()
    {
        using (_principalAccessor.AsClient(BlobManagementApplicationTestModule.AllowedClient))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => ListAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateAsync("Folder", null));
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

    private async Task<IReadOnlyList<BlobManagementDirectoryDto>> ListAsync(Guid? parentId)
    {
        return (await _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container, ParentId = parentId })).Items;
    }

    /// <summary>Stores a file as an application in this process does.</summary>
    private Task<BlobManagementFileDto> StoreAsync(string containerName, Guid? directoryId)
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
}
