using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Abp.BlobManagement.Public;
using Dignite.Abp.BlobManagement.Usage;
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
/// The application services end to end, against the database provider of the concrete test class: files as an
/// application in this process keeps them (<see cref="IBlobManagementFileService"/>, trusted), the file API as a client
/// and as a user reach it (<see cref="IBlobManagementFileAppService"/>), containers, usage and public reads - with
/// grants written through ABP's permission managers.
/// </summary>
public abstract class BlobManagementFiles_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Client = BlobManagementApplicationTestModule.AllowedClient;

    private readonly IBlobManagementContainerAppService _containers;
    private readonly IBlobManagementFileService _files;
    private readonly IBlobManagementFileAppService _fileApi;
    private readonly IBlobManagementUsageAppService _usage;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    protected BlobManagementFiles_Tests()
    {
        _containers = GetRequiredService<IBlobManagementContainerAppService>();
        _files = GetRequiredService<IBlobManagementFileService>();
        _fileApi = GetRequiredService<IBlobManagementFileAppService>();
        _usage = GetRequiredService<IBlobManagementUsageAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task An_Application_Should_Keep_Files_By_Id_For_Its_User()
    {
        var bytes = BlobManagementTestFiles.Pdf("alice " + Guid.NewGuid());
        var folder = await CreateDirectoryAsync("Reports " + Guid.NewGuid());

        // The application's user holds no permission of Blob Management: the application's rules ran before the call.
        using (As(_alice))
        {
            var stored = await _files.CreateAsync(NewFile(BlobManagementTestContainers.Documents, "report.pdf", bytes, folder));
            stored.Name.ShouldBe("report.pdf");
            stored.MimeType.ShouldBe("application/pdf");
            stored.DirectoryId.ShouldBe(folder);
            stored.IsAddressedByName.ShouldBeFalse();
            stored.Url.ShouldBeNull();
            // Counted for the application's current user, and stored by no client.
            stored.CreatorId.ShouldBe(_alice);
            stored.ClientId.ShouldBeNull();

            (await _files.GetAsync(stored.Id)).Name.ShouldBe("report.pdf");
            (await ReadAsync(await _files.GetContentAsync(stored.Id))).ShouldBe(bytes);

            var listed = await _files.GetListAsync(new GetBlobManagementFilesInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                DirectoryId = folder,
                Filter = "REPORT"
            });
            listed.TotalCount.ShouldBe(1);
            listed.Items.Single().Id.ShouldBe(stored.Id);

            (await _files.UpdateAsync(stored.Id, new UpdateBlobManagementFileInput { Name = " final.pdf " })).Name.ShouldBe("final.pdf");
            (await _files.MoveAsync(stored.Id, new MoveBlobManagementFileInput { DirectoryId = null })).DirectoryId.ShouldBeNull();

            // A file never changes containers: a directory that is not its container's is not found.
            var elsewhere = await Should.ThrowAsync<BusinessException>(() =>
                _files.MoveAsync(stored.Id, new MoveBlobManagementFileInput { DirectoryId = Guid.NewGuid() }));
            elsewhere.Code.ShouldBe(BlobManagementErrorCodes.DirectoryNotFound);

            await _files.DeleteAsync(stored.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _files.GetAsync(stored.Id));
            // Deleting what is gone already is not an error.
            await _files.DeleteAsync(stored.Id);

            // The application may name the user a file is counted for.
            var forBob = NewFile(BlobManagementTestContainers.Documents, "bob.pdf");
            forBob.CreatorId = _bob;
            (await _files.CreateAsync(forBob)).CreatorId.ShouldBe(_bob);

            var unknown = await Should.ThrowAsync<BusinessException>(() =>
                _files.CreateAsync(NewFile(BlobManagementTestContainers.NotManaged, "a.pdf")));
            unknown.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
        }
    }

    [Fact]
    public async Task The_File_Api_Should_Admit_A_Client_Granted_The_Containers_Files_For_Everything()
    {
        await GrantFilesToClientAsync(BlobManagementTestContainers.Documents, Client);
        var bytes = BlobManagementTestFiles.Pdf("client " + Guid.NewGuid());
        var folder = await CreateDirectoryAsync("Inbox " + Guid.NewGuid());
        var notTheClients = await StoreAsync(BlobManagementTestContainers.Files, "elsewhere.pdf");

        BlobManagementFileDto stored;
        using (AsClient(Client))
        {
            var input = NewFile(BlobManagementTestContainers.Documents, "contract.pdf", bytes);
            input.CreatorId = _alice;
            stored = await _fileApi.CreateAsync(input);

            // The client is recorded as the writer, and the user it named as the one the file is counted for.
            stored.ClientId.ShouldBe(Client);
            stored.CreatorId.ShouldBe(_alice);

            (await _fileApi.GetAsync(stored.Id)).Name.ShouldBe("contract.pdf");
            (await ReadAsync(await _fileApi.GetContentAsync(stored.Id))).ShouldBe(bytes);
            (await _fileApi.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementTestContainers.Documents, Filter = "contract" }))
                .Items.Select(f => f.Id).ShouldContain(stored.Id);
            (await _fileApi.UpdateAsync(stored.Id, new UpdateBlobManagementFileInput { Name = "signed.pdf" })).Name.ShouldBe("signed.pdf");
            (await _fileApi.MoveAsync(stored.Id, new MoveBlobManagementFileInput { DirectoryId = folder })).DirectoryId.ShouldBe(folder);

            // A container the client is not granted: nothing is stored or listed there, and a file in it is not found -
            // its id tells nothing.
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.CreateAsync(NewFile(BlobManagementTestContainers.Files, "a.pdf")));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _fileApi.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementTestContainers.Files }));
            await Should.ThrowAsync<EntityNotFoundException>(() => _fileApi.GetAsync(notTheClients.Id));
            await Should.ThrowAsync<EntityNotFoundException>(() => _fileApi.GetContentAsync(notTheClients.Id));
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _fileApi.UpdateAsync(notTheClients.Id, new UpdateBlobManagementFileInput { Name = "taken.pdf" }));
            await _fileApi.DeleteAsync(notTheClients.Id);
        }

        (await _files.GetAsync(notTheClients.Id)).Name.ShouldBe("elsewhere.pdf");

        // A client's grant is the deployment's: it holds in every tenant.
        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        using (AsClient(Client))
        {
            (await _fileApi.CreateAsync(NewFile(BlobManagementTestContainers.Documents, "tenant.pdf"))).ClientId.ShouldBe(Client);
        }

        // Another client is not granted the container.
        using (AsClient("another-app"))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.CreateAsync(NewFile(BlobManagementTestContainers.Documents, "a.pdf")));
            await Should.ThrowAsync<EntityNotFoundException>(() => _fileApi.GetAsync(stored.Id));
            await _fileApi.DeleteAsync(stored.Id);
        }

        using (AsClient(Client))
        {
            await _fileApi.DeleteAsync(stored.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _fileApi.GetAsync(stored.Id));
            // Deleting what is gone already is not an error.
            await _fileApi.DeleteAsync(stored.Id);
        }
    }

    [Fact]
    public async Task The_File_Api_Should_Let_A_User_Read_With_The_Management_Permission_And_Change_Nothing()
    {
        await GrantAsync(_admin, BlobManagementPermissions.GetAll());
        var bytes = BlobManagementTestFiles.Pdf("kept " + Guid.NewGuid());
        var file = await StoreAsync(BlobManagementTestContainers.Files, "kept.pdf", bytes);

        // A signed-in user without the permission reads nothing, and learns nothing about an id.
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.GetAsync(file.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.GetAsync(Guid.NewGuid()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.GetContentAsync(file.Id));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _fileApi.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementTestContainers.Files }));
        }

        // The management permission reads every container, whatever clients it lists.
        using (As(_admin))
        {
            (await _fileApi.GetAsync(file.Id)).Name.ShouldBe("kept.pdf");
            (await ReadAsync(await _fileApi.GetContentAsync(file.Id))).ShouldBe(bytes);
            (await _fileApi.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementTestContainers.Files, Filter = "kept" }))
                .Items.Select(f => f.Id).ShouldContain(file.Id);
            await Should.ThrowAsync<EntityNotFoundException>(() => _fileApi.GetAsync(Guid.NewGuid()));

            // It changes nothing: a file is its application's, whatever the user holds here.
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.CreateAsync(NewFile(BlobManagementTestContainers.Documents, "a.pdf")));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _fileApi.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "mine.pdf" }));
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _fileApi.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = null }));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _fileApi.DeleteAsync(file.Id));
        }

        (await _files.GetAsync(file.Id)).Name.ShouldBe("kept.pdf");
    }

    [Fact]
    public async Task The_Services_Should_Be_Closed_When_The_Tenants_BlobManagement_Is_Off()
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
                         () => _files.GetAsync(Guid.NewGuid()),
                         () => _files.CreateAsync(NewFile(BlobManagementTestContainers.Documents, "a.pdf"))
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
    public async Task A_Container_Should_Tell_What_It_Takes_And_What_The_Caller_May_Do_With_Its_Directories()
    {
        // The container service serves users: a client reads a container's limits from its own configuration.
        using (AsClient(Client))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _containers.GetListAsync());
        }

        // Open to every signed-in user: a business system's file input needs the limits before it offers an upload.
        using (As(_bob))
        {
            var documents = await _containers.GetAsync(BlobManagementTestContainers.Documents);
            documents.MaxFileSize.ShouldBeGreaterThan(0);
            documents.AllowDirectories.ShouldBeTrue();
            documents.Access.View.ShouldBeFalse();
            documents.Access.CreateDirectories.ShouldBeFalse();

            (await _containers.GetAsync(BlobManagementTestContainers.Files)).AllowDirectories.ShouldBeFalse();
            (await _containers.GetListAsync()).Items.ShouldBeEmpty();

            var unknown = await Should.ThrowAsync<BusinessException>(() => _containers.GetAsync(BlobManagementTestContainers.NotManaged));
            unknown.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
        }

        await GrantOnContainerAsync(BlobManagementTestContainers.Documents, _bob,
            BlobManagementPermissions.Container.View, BlobManagementPermissions.Container.CreateDirectories);

        using (As(_bob))
        {
            var listed = (await _containers.GetListAsync()).Items.Single();
            listed.Name.ShouldBe(BlobManagementTestContainers.Documents);
            listed.Access.View.ShouldBeTrue();
            listed.Access.CreateDirectories.ShouldBeTrue();
            listed.Access.DeleteDirectories.ShouldBeFalse();
        }

        // The management permission sees every managed container.
        await GrantAsync(_admin, BlobManagementPermissions.Containers.Default);
        using (As(_admin))
        {
            var containers = (await _containers.GetListAsync()).Items;
            containers.Select(c => c.Name).ShouldContain(BlobManagementTestContainers.Files);
            containers.Select(c => c.Name).ShouldNotContain(BlobManagementTestContainers.NotManaged);

            var documents = containers.Single(c => c.Name == BlobManagementTestContainers.Documents);
            documents.Access.View.ShouldBeTrue();
            // Seeing is not changing: the directory permissions are granted one by one.
            documents.Access.CreateDirectories.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Usage_Should_Be_Read_With_The_Management_Permission()
    {
        using (As(_bob))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _usage.GetAsync());
            await Should.ThrowAsync<AbpAuthorizationException>(() => _usage.GetUsersAsync(new GetBlobManagementUserUsagesInput()));
            await Should.ThrowAsync<AbpAuthorizationException>(() => _usage.GetClientsAsync(new GetBlobManagementClientUsagesInput()));
        }

        using (AsClient(Client))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _usage.GetAsync());
        }

        var tenantId = Guid.NewGuid();
        GetRequiredService<FakeFeatureStore>().SetForTenant(tenantId, BlobManagementFeatures.StorageSize, "5");

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await GrantAsync(_admin, BlobManagementPermissions.Containers.Default);
            var shared = BlobManagementTestFiles.Pdf("shared " + Guid.NewGuid());
            var other = BlobManagementTestFiles.Pdf("other " + Guid.NewGuid() + new string('x', 200));

            // Alice and Bob keep the same bytes in one container; Bob keeps another file in another.
            await StoreAsync(BlobManagementTestContainers.Documents, "alice.pdf", shared, _alice);
            await StoreAsync(BlobManagementTestContainers.Documents, "bob.pdf", shared, _bob);
            await StoreAsync(BlobManagementTestContainers.Files, "bob-too.pdf", other, _bob);

            using (As(_admin))
            {
                var usage = await _usage.GetAsync();
                usage.StoredBytes.ShouldBe(shared.Length + other.Length);
                usage.StorageSize.ShouldBe(5L * 1024 * 1024);
                usage.FileCount.ShouldBe(3);

                // Every managed container has a row, the empty ones too, and they add up to the tenant.
                var documents = usage.Containers.Single(c => c.ContainerName == BlobManagementTestContainers.Documents);
                documents.StoredBytes.ShouldBe(shared.Length);
                documents.FileCount.ShouldBe(2);
                usage.Containers.Single(c => c.ContainerName == BlobManagementTestContainers.Files).StoredBytes.ShouldBe(other.Length);
                usage.Containers.Single(c => c.ContainerName == BlobManagementTestContainers.Archive).StoredBytes.ShouldBe(0);
                usage.Containers.Select(c => c.ContainerName).ShouldNotContain(BlobManagementTestContainers.NotManaged);
                usage.Containers.Sum(c => c.StoredBytes).ShouldBe(usage.StoredBytes);

                // A user is counted for each of their files: the shared bytes count for both.
                var users = await _usage.GetUsersAsync(new GetBlobManagementUserUsagesInput());
                users.TotalCount.ShouldBe(2);
                users.Items.Select(u => u.UserId).ShouldBe(new Guid?[] { _bob, _alice });
                users.Items[0].UploadedBytes.ShouldBe(shared.Length + other.Length);
                users.Items[0].FileCount.ShouldBe(2);
                users.Items[1].UploadedBytes.ShouldBe(shared.Length);
                // This host has no user lookup: a user is told by id.
                users.Items[0].UserName.ShouldBeNull();

                var inDocuments = await _usage.GetUsersAsync(
                    new GetBlobManagementUserUsagesInput { ContainerName = BlobManagementTestContainers.Documents, MaxResultCount = 1 });
                inDocuments.TotalCount.ShouldBe(2);
                inDocuments.Items.ShouldHaveSingleItem().UploadedBytes.ShouldBe(shared.Length);

                // Stored in process: by no client.
                var clients = await _usage.GetClientsAsync(new GetBlobManagementClientUsagesInput());
                var inProcess = clients.Items.ShouldHaveSingleItem();
                inProcess.ClientId.ShouldBeNull();
                inProcess.UploadedBytes.ShouldBe(2L * shared.Length + other.Length);
                inProcess.FileCount.ShouldBe(3);
            }
        }
    }

    [Fact]
    public async Task A_File_An_Application_Saved_Under_A_Name_Should_Be_Listed_And_Not_Renamed_Or_Moved()
    {
        var name = "themes/" + Guid.NewGuid().ToString("N") + ".pdf";
        var saved = await StoreSystemFileAsync(name, BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));

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
        var moved = await Should.ThrowAsync<BusinessException>(() =>
            _files.MoveAsync(saved.Id, new MoveBlobManagementFileInput { DirectoryId = null }));
        moved.Code.ShouldBe(BlobManagementErrorCodes.FileAddressedByName);

        await _files.DeleteAsync(saved.Id);

        (await WithUnitOfWorkAsync(() => GetRequiredService<FileDescriptorManager>().FindAsync(BlobManagementTestContainers.Files, name)))
            .ShouldBeNull();
    }

    [Fact]
    public async Task A_Public_File_Kept_By_Id_Should_Be_Read_By_The_Physical_Blob_Name()
    {
        var file = await StoreAsync(BlobManagementTestContainers.Public, "poster.pdf");

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

        // For a BLOB saved under a name the name is the address: an overwrite keeps it and changes the tag behind it.
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

    /// <summary>Stores a file as an application in this process does, counted for <paramref name="creatorId"/>.</summary>
    private Task<BlobManagementFileDto> StoreAsync(string containerName, string name, byte[]? bytes = null, Guid? creatorId = null)
    {
        var input = NewFile(containerName, name, bytes);
        input.CreatorId = creatorId;
        return _files.CreateAsync(input);
    }

    private static CreateBlobManagementFileInput NewFile(string containerName, string name, byte[]? bytes = null, Guid? directoryId = null)
    {
        return new CreateBlobManagementFileInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            File = new RemoteStreamContent(
                new MemoryStream(bytes ?? BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), name, "application/pdf")
        };
    }

    /// <summary>A directory at the top of the documents container, made as the domain makes one.</summary>
    private async Task<Guid> CreateDirectoryAsync(string name)
    {
        return (await WithUnitOfWorkAsync(() =>
            GetRequiredService<DirectoryManager>().CreateAsync(BlobManagementTestContainers.Documents, name, null))).Id;
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

    private IDisposable AsClient(string clientId)
    {
        return _principalAccessor.AsClient(clientId);
    }

    private Task GrantAsync(Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().GrantAsync(userId, permissions));
    }

    private Task GrantFilesToClientAsync(string containerName, string clientId)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionManager>().GrantFilesToClientAsync(containerName, clientId));
    }

    private Task GrantOnContainerAsync(string containerName, Guid userId, params string[] permissions)
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IResourcePermissionManager>().GrantOnContainerAsync(containerName, userId, permissions));
    }
}
