using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Abp.BlobManagement.Public;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The file API's application services end to end - containers and files - as signed-in users, with grants
/// written through ABP's permission managers, against the database provider of the concrete test class.
/// </summary>
public abstract class BlobManagementFiles_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBlobManagementContainerAppService _containers;
    private readonly IBlobManagementFileAppService _files;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    protected BlobManagementFiles_Tests()
    {
        _containers = GetRequiredService<IBlobManagementContainerAppService>();
        _files = GetRequiredService<IBlobManagementFileAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task A_User_Should_Upload_And_Read_Their_Own_Files()
    {
        await GrantAsync(_alice, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.UploadFiles);
        var bytes = BlobManagementTestFiles.Pdf("alice " + Guid.NewGuid());

        using (As(_alice))
        {
            var uploaded = await UploadAsync(BlobManagementTestContainers.Documents, "report.pdf", bytes);
            uploaded.Name.ShouldBe("report.pdf");
            uploaded.MimeType.ShouldBe("application/pdf");
            uploaded.CreatorId.ShouldBe(_alice);
            uploaded.Url.ShouldBeNull();

            (await _files.GetAsync(uploaded.Id)).Name.ShouldBe("report.pdf");
            (await ReadAsync(await _files.GetContentAsync(uploaded.Id))).ShouldBe(bytes);

            await _files.DeleteAsync(uploaded.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _files.GetAsync(uploaded.Id));
        }
    }

    [Fact]
    public async Task Any_Signed_In_User_Should_Read_A_File_And_Only_Its_Uploader_Or_A_Deleter_Delete_It()
    {
        await GrantAsync(_alice, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.UploadFiles);
        var bytes = BlobManagementTestFiles.Pdf("shared " + Guid.NewGuid());

        BlobManagementFileDto plan;
        BlobManagementFileDto draft;
        using (As(_alice))
        {
            plan = await UploadAsync(BlobManagementTestContainers.Documents, "plan.pdf", bytes);
            draft = await UploadAsync(BlobManagementTestContainers.Documents, "draft.pdf");
        }

        // A signed-in user with the id reads the file, and nothing finer is asked.
        using (As(_bob))
        {
            (await _files.GetAsync(plan.Id)).Name.ShouldBe("plan.pdf");
            (await ReadAsync(await _files.GetContentAsync(plan.Id))).ShouldBe(bytes);
            await Should.ThrowAsync<AbpAuthorizationException>(() => _files.DeleteAsync(plan.Id));
        }

        // The uploader deletes their own file without the permission to delete files.
        using (As(_alice))
        {
            await _files.DeleteAsync(draft.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _files.GetAsync(draft.Id));
        }

        await GrantAsync(_bob, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.DeleteFiles);
        using (As(_bob))
        {
            await _files.DeleteAsync(plan.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _files.GetAsync(plan.Id));
        }
    }

    [Fact]
    public async Task Uploading_Should_Take_A_Permission()
    {
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => UploadAsync(BlobManagementTestContainers.Documents, "a.pdf"));
        }
    }

    [Fact]
    public async Task The_File_Api_Should_Be_Closed_When_The_Tenants_BlobManagement_Is_Off()
    {
        var off = Guid.NewGuid();
        GetRequiredService<FakeFeatureStore>().SetForTenant(off, BlobManagementFeatures.Enable, "false");
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(off))
        using (As(_alice))
        {
            foreach (var call in new Func<Task>[]
                     {
                         () => _containers.GetListAsync(),
                         () => _files.GetAsync(Guid.NewGuid())
                     })
            {
                var refused = await Should.ThrowAsync<AbpAuthorizationException>(call);
                refused.Code.ShouldBe(AbpFeatureErrorCodes.AtLeastOneOfTheseFeaturesMustBeEnabled);
            }
        }

        // Another tenant has the feature's default, which is on.
        using (currentTenant.Change(Guid.NewGuid()))
        using (As(_alice))
        {
            (await _containers.GetAsync(BlobManagementTestContainers.Documents)).Name.ShouldBe(BlobManagementTestContainers.Documents);
        }
    }

    [Fact]
    public async Task The_File_Api_Should_Serve_Users_And_Managed_Containers_Only()
    {
        var client = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.ClientId, "site-app") }, "Test"));
        using (_principalAccessor.Change(client))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => UploadAsync(BlobManagementTestContainers.Documents, "a.pdf"));
        }

        await GrantAsync(_alice, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.UploadFiles);
        using (As(_alice))
        {
            var unknown = await Should.ThrowAsync<BusinessException>(() => UploadAsync(BlobManagementTestContainers.NotManaged, "a.pdf"));
            unknown.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);

            var containers = (await _containers.GetListAsync()).Items.Select(c => c.Name).ToList();
            containers.ShouldContain(BlobManagementTestContainers.Documents);
            containers.ShouldContain(BlobManagementTestContainers.Files);
            containers.ShouldNotContain(BlobManagementTestContainers.NotManaged);
        }
    }

    [Fact]
    public async Task A_Container_Should_Tell_What_It_Takes_And_What_The_Caller_May_Do()
    {
        // Open to every signed-in user: a file input needs the limits before it offers an upload.
        using (As(_bob))
        {
            var documents = await _containers.GetAsync(BlobManagementTestContainers.Documents);
            documents.MaxFileSize.ShouldBeGreaterThan(0);
            documents.AllowDirectories.ShouldBeTrue();
            documents.Access.View.ShouldBeFalse();
            documents.Access.UploadFiles.ShouldBeFalse();

            (await _containers.GetAsync(BlobManagementTestContainers.Files)).AllowDirectories.ShouldBeFalse();
            (await _containers.GetListAsync()).Items.ShouldBeEmpty();
        }

        await GrantOnContainerAsync(BlobManagementTestContainers.Documents, _bob,
            BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.UploadFiles);

        using (As(_bob))
        {
            var listed = (await _containers.GetListAsync()).Items.Single();
            listed.Name.ShouldBe(BlobManagementTestContainers.Documents);
            listed.Access.View.ShouldBeTrue();
            listed.Access.UploadFiles.ShouldBeTrue();
            listed.Access.DeleteFiles.ShouldBeFalse();

            (await UploadAsync(BlobManagementTestContainers.Documents, "granted.pdf")).ContainerName.ShouldBe(BlobManagementTestContainers.Documents);
            // The grant is on that container only.
            await Should.ThrowAsync<AbpAuthorizationException>(() => UploadAsync(BlobManagementTestContainers.Archive, "a.pdf"));
        }
    }

    [Fact]
    public async Task Files_Should_Be_Listed_Renamed_And_Moved_By_The_Containers_Permissions()
    {
        var admin = Guid.NewGuid();
        await GrantAsync(admin, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.CreateDirectories);
        await GrantOnContainerAsync(BlobManagementTestContainers.Documents, _alice,
            BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.UploadFiles);

        Guid folderId;
        using (As(admin))
        {
            folderId = (await GetRequiredService<IBlobManagementDirectoryAppService>().CreateAsync(new CreateBlobManagementDirectoryInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                Name = "Reports " + Guid.NewGuid()
            })).Id;
        }

        BlobManagementFileDto file;
        using (As(_alice))
        {
            file = await _files.CreateAsync(NewUpload(BlobManagementTestContainers.Documents, "draft.pdf", directoryId: folderId));
            file.DirectoryId.ShouldBe(folderId);
            file.IsAddressedByName.ShouldBeFalse();

            var listed = await _files.GetListAsync(new GetBlobManagementFilesInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                DirectoryId = folderId,
                Filter = "DRAFT"
            });
            listed.TotalCount.ShouldBe(1);
            listed.Items.Single().Id.ShouldBe(file.Id);

            // Uploading is not renaming or moving.
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _files.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "final.pdf" }));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = null }));
        }

        // Nothing in the container without a grant on it: not even the listing.
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _files.GetListAsync(new GetBlobManagementFilesInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                DirectoryId = folderId
            }));
        }

        await GrantOnContainerAsync(BlobManagementTestContainers.Documents, _alice, BlobManagementPermissions.Container.UpdateFiles);

        using (As(_alice))
        {
            (await _files.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = " final.pdf " })).Name.ShouldBe("final.pdf");
            (await _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = null })).DirectoryId.ShouldBeNull();
            (await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementTestContainers.Documents }))
                .Items.Select(f => f.Id).ShouldContain(file.Id);

            // A directory of another container is not found: a file never changes containers.
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = Guid.NewGuid() }));
        }
    }

    [Fact]
    public async Task A_File_An_Application_Saved_Should_Be_Listed_And_Not_Renamed_Or_Moved()
    {
        await GrantAsync(_alice,
            BlobManagementPermissions.Containers.Default,
            BlobManagementPermissions.Containers.UpdateFiles,
            BlobManagementPermissions.Containers.DeleteFiles);
        var name = "themes/" + Guid.NewGuid().ToString("N") + ".pdf";
        var saved = await StoreSystemFileAsync(name, BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));

        using (As(_alice))
        {
            var listed = (await _files.GetListAsync(new GetBlobManagementFilesInput
            {
                ContainerName = BlobManagementTestContainers.Files,
                Filter = name
            })).Items.Single();
            listed.Id.ShouldBe(saved.Id);
            listed.IsAddressedByName.ShouldBeTrue();
            (await _files.GetAsync(saved.Id)).Name.ShouldBe(name);

            // Its name is how its application reads it back.
            var renamed = await Should.ThrowAsync<BusinessException>(() =>
                _files.UpdateAsync(saved.Id, new UpdateBlobManagementFileInput { Name = "other.pdf" }));
            renamed.Code.ShouldBe(BlobManagementErrorCodes.FileAddressedByName);

            await _files.DeleteAsync(saved.Id);
        }

        (await WithUnitOfWorkAsync(() => GetRequiredService<FileDescriptorManager>().FindAsync(BlobManagementTestContainers.Files, name)))
            .ShouldBeNull();
    }

    [Fact]
    public async Task An_Uploaded_Public_File_Should_Be_Read_By_The_Physical_Blob_Name()
    {
        await GrantAsync(_alice, BlobManagementPermissions.Containers.Default, BlobManagementPermissions.Containers.UploadFiles);

        BlobManagementFileDto file;
        using (As(_alice))
        {
            file = await UploadAsync(BlobManagementTestContainers.Public, "poster.pdf");
        }

        file.Url.ShouldNotBeNull();
        var blobName = file.Url!.Split('/').Last();
        file.Url.ShouldBe(BlobManagementPublicUrl.Build(BlobManagementTestContainers.Public, blobName));

        var publicFiles = GetRequiredService<IBlobManagementPublicAppService>();
        (await publicFiles.ReadAsync(BlobManagementTestContainers.Public, blobName, new BlobManagementPublicReadInput(), HeadersOnly()))
            .File.Name.ShouldBe("poster.pdf");

        // The display name is not an address there: names may repeat.
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            publicFiles.ReadAsync(BlobManagementTestContainers.Public, "poster.pdf", new BlobManagementPublicReadInput(), HeadersOnly()));
    }

    [Fact]
    public async Task A_Public_Read_Should_Read_The_Bytes_Only_When_They_Are_Wanted()
    {
        var name = Guid.NewGuid() + ".png";
        var bytes = BlobManagementTestFiles.Png(40, 20, seed: 11);
        var stored = await StoreSystemFileAsync(name, bytes);
        var reads = GetRequiredService<RacingBlobManagementPublicAppService>();

        var head = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(), HeadersOnly());
        head.NotModified.ShouldBeFalse();
        head.Content.ShouldBeNull();
        head.File.Name.ShouldBe(name);
        head.File.MimeType.ShouldBe("image/png");
        head.File.Size.ShouldBe(bytes.LongLength);
        head.File.ContentId.ShouldBe(stored.BlobName);
        head.File.EntityTag.ShouldBe(stored.BlobName);

        // The client's copy is current: answered from the lookup, nothing read.
        var current = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(),
            new BlobManagementPublicReadRequest { IfNoneMatch = { "something-else", head.File.EntityTag } });
        current.NotModified.ShouldBeTrue();
        current.Content.ShouldBeNull();

        var any = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(),
            new BlobManagementPublicReadRequest { IfNoneMatchAny = true });
        any.NotModified.ShouldBeTrue();
        any.Content.ShouldBeNull();

        // A stale copy gets the bytes.
        var stale = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(),
            new BlobManagementPublicReadRequest { IfNoneMatch = { "stale" } });
        stale.NotModified.ShouldBeFalse();
        stale.Content.ShouldNotBeNull();
        stale.Content!.ContentType.ShouldBe("image/png");
        (await ReadAsync(stale.Content)).ShouldBe(bytes);
    }

    [Fact]
    public async Task A_Public_Reads_Entity_Tag_Should_Change_With_The_Size_And_When_The_File_Is_Overwritten()
    {
        var name = Guid.NewGuid() + ".png";
        await StoreSystemFileAsync(name, BlobManagementTestFiles.Png(200, 100, seed: 12));
        var reads = GetRequiredService<RacingBlobManagementPublicAppService>();

        var plain = (await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(), HeadersOnly())).File.EntityTag;
        (await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput { Width = 64 }, HeadersOnly()))
            .File.EntityTag.ShouldBe(plain + "-64x0");
        (await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput { Width = 64, Height = 64 }, HeadersOnly()))
            .File.EntityTag.ShouldBe(plain + "-64x64");

        // In a system container the name is the address: an overwrite keeps it and changes the tag behind it.
        await StoreSystemFileAsync(name, BlobManagementTestFiles.Png(200, 100, seed: 13), overrideExisting: true);

        var overwritten = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(),
            new BlobManagementPublicReadRequest { IfNoneMatch = { plain } });
        overwritten.File.EntityTag.ShouldNotBe(plain);
        overwritten.NotModified.ShouldBeFalse();
        overwritten.Content.ShouldNotBeNull();
        overwritten.Content!.Dispose();
    }

    [Fact]
    public async Task A_File_Overwritten_During_A_Public_Read_Should_Not_Mix_Into_It()
    {
        // No extension: the same address takes a picture first and a PDF after.
        var name = "race-" + Guid.NewGuid();
        var oldBytes = BlobManagementTestFiles.Png(16, 16, seed: 14);
        var newBytes = BlobManagementTestFiles.Pdf("new " + Guid.NewGuid());
        var old = await StoreSystemFileAsync(name, oldBytes);

        // Shares the old file's BLOB (dedup), so its bytes outlive the overwrite and the read can still serve them.
        await StoreSystemFileAsync(name + "-keeper", oldBytes);

        var reads = GetRequiredService<RacingBlobManagementPublicAppService>();

        // The overwrite lands after the read found the file and before it opens the bytes - where a second lookup
        // used to find the new file and mix its bytes and type into the old file's tag and disposition. (It runs in
        // the read's unit of work, as a child; a concurrent request would commit its own, with the same effect.)
        reads.AfterLookup = () => StoreSystemFileAsync(name, newBytes, overrideExisting: true);

        var result = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest());

        reads.Lookups.ShouldBe(1);
        result.File.ContentId.ShouldBe(old.BlobName);
        result.File.EntityTag.ShouldBe(old.BlobName);
        result.File.MimeType.ShouldBe("image/png");
        result.Content.ShouldNotBeNull();
        result.Content!.ContentType.ShouldBe("image/png");
        (await ReadAsync(result.Content)).ShouldBe(oldBytes);

        // The next read is the new file, whole.
        var next = await reads.ReadAsync(BlobManagementTestContainers.Files, name, new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest());
        next.File.MimeType.ShouldBe("application/pdf");
        next.File.EntityTag.ShouldNotBe(result.File.EntityTag);
        next.Content!.ContentType.ShouldBe("application/pdf");
        (await ReadAsync(next.Content)).ShouldBe(newBytes);
    }

    private Task<FileDescriptor> StoreSystemFileAsync(string name, byte[] bytes, bool overrideExisting = false)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await GetRequiredService<FileDescriptorManager>()
                .CreateAsync(BlobManagementTestContainers.Files, name, stream, overrideExisting);
        });
    }

    private static BlobManagementPublicReadRequest HeadersOnly()
    {
        return new BlobManagementPublicReadRequest { HeadersOnly = true };
    }

    private Task<BlobManagementFileDto> UploadAsync(string containerName, string name, byte[]? bytes = null)
    {
        return _files.CreateAsync(NewUpload(containerName, name, bytes));
    }

    private static CreateBlobManagementFileInput NewUpload(string containerName, string name, byte[]? bytes = null, Guid? directoryId = null)
    {
        return new CreateBlobManagementFileInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            File = new RemoteStreamContent(
                new MemoryStream(bytes ?? BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), name, "application/pdf")
        };
    }

    private static async Task<byte[]> ReadAsync(IRemoteStreamContent content)
    {
        using (content)
        {
            using var memory = new MemoryStream();
            await content.GetStream().CopyToAsync(memory);
            return memory.ToArray();
        }
    }

    private IDisposable As(Guid userId)
    {
        return _principalAccessor.As(userId);
    }

    private Task GrantAsync(Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().GrantAsync(userId, permissions));
    }

    private Task GrantOnContainerAsync(string containerName, Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionManager>().GrantOnContainerAsync(containerName, userId, permissions));
    }
}
