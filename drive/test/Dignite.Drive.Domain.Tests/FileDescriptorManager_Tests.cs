using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The storage core end to end: bytes through <c>IBlobContainer</c> and its pipeline contributors, rows through
/// <see cref="FileDescriptorManager"/> and the database provider of the concrete test class.
/// </summary>
public abstract class FileDescriptorManager_Tests<TStartupModule> : DriveDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected FileDescriptorManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_Under_The_Callers_Name_Tenant_Scoped()
    {
        var bytes = DriveTestFiles.Pdf("tenant scoped");
        var tenantId = Guid.NewGuid();

        using (_currentTenant.Change(tenantId))
        {
            var descriptor = await CreateAsync(DriveTestContainers.Files, "2026/11/notes.pdf", bytes);

            descriptor.TenantId.ShouldBe(tenantId);
            descriptor.ContainerName.ShouldBe(DriveTestContainers.Files);
            descriptor.Name.ShouldBe("2026/11/notes.pdf");
            Regex.IsMatch(descriptor.BlobName, "^[0-9a-f]{32}\\.pdf$").ShouldBeTrue();
            descriptor.MimeType.ShouldBe("application/pdf");
            descriptor.Size.ShouldBe(bytes.Length);
            descriptor.Hash.Length.ShouldBe(64);
            descriptor.OwnsBlob().ShouldBeTrue();
            descriptor.DirectoryId.ShouldBeNull();

            var found = await FindAsync(DriveTestContainers.Files, "2026/11/notes.pdf");
            found.ShouldNotBeNull();
            found!.Id.ShouldBe(descriptor.Id);
            (await ReadAsync(found)).ShouldBe(bytes);
        }

        // Another tenant does not see the row, however it addresses it.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await FindAsync(DriveTestContainers.Files, "2026/11/notes.pdf")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Record_The_Writing_Client()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(DriveTestFiles.Pdf("client " + Guid.NewGuid()));
            return await _fileDescriptorManager.CreateAsync(DriveTestContainers.Files, "client.pdf", stream, clientId: "site-app");
        });

        descriptor.ClientId.ShouldBe("site-app");
    }

    [Fact]
    public async Task Should_Refuse_A_Container_The_Drive_Did_Not_Declare()
    {
        var create = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(DriveTestContainers.NotDrive, "notes.pdf", DriveTestFiles.Pdf()));
        create.Code.ShouldBe(DriveErrorCodes.ContainerNotFound);
        create.Data["ContainerName"].ShouldBe(DriveTestContainers.NotDrive);

        var find = await Should.ThrowAsync<BusinessException>(() => FindAsync(DriveTestContainers.NotDrive, "notes.pdf"));
        find.Code.ShouldBe(DriveErrorCodes.ContainerNotFound);
    }

    [Fact]
    public async Task Should_Refuse_An_Existing_Name_Unless_Overriding()
    {
        var name = "existing-" + Guid.NewGuid().ToString("N") + ".pdf";
        await CreateAsync(DriveTestContainers.Files, name, DriveTestFiles.Pdf("first " + name));

        await Should.ThrowAsync<BlobAlreadyExistsException>(() =>
            CreateAsync(DriveTestContainers.Files, name, DriveTestFiles.Pdf("second " + name)));
    }

    [Fact]
    public async Task Overriding_Should_Replace_The_Descriptor_And_Remove_The_Old_Blob()
    {
        var name = "replaced-" + Guid.NewGuid().ToString("N") + ".pdf";
        var first = await CreateAsync(DriveTestContainers.Files, name, DriveTestFiles.Pdf("first " + name));
        var secondBytes = DriveTestFiles.Pdf("second " + name);

        var second = await CreateAsync(DriveTestContainers.Files, name, secondBytes, overrideExisting: true);

        second.Id.ShouldNotBe(first.Id);
        second.BlobName.ShouldNotBe(first.BlobName);
        (await FindAsync(DriveTestContainers.Files, name))!.Id.ShouldBe(second.Id);
        (await ReadAsync(second)).ShouldBe(secondBytes);
        (await BlobExistsAsync(DriveTestContainers.Files, first.BlobName)).ShouldBeFalse();
    }

    [Fact]
    public async Task Overriding_With_The_Same_Bytes_Should_Keep_The_Blob()
    {
        var name = "same-" + Guid.NewGuid().ToString("N") + ".pdf";
        var bytes = DriveTestFiles.Pdf(name);
        var first = await CreateAsync(DriveTestContainers.Files, name, bytes);

        var second = await CreateAsync(DriveTestContainers.Files, name, bytes, overrideExisting: true);

        second.BlobName.ShouldBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);
    }

    /// <summary>
    /// The same bytes under two names keep one BLOB: the second descriptor shares the first one's, and the lookup runs
    /// before the save, so nothing is written for it.
    /// </summary>
    [Fact]
    public async Task Should_Dedup_Identical_Content_Into_One_Blob()
    {
        var bytes = DriveTestFiles.Pdf("dedup " + Guid.NewGuid());

        var first = await CreateAsync(DriveTestContainers.Files, "first.pdf", bytes);
        (await BlobExistsAsync(DriveTestContainers.Files, first.BlobName)).ShouldBeTrue();
        var second = await CreateAsync(DriveTestContainers.Files, "second.pdf", bytes);

        second.BlobName.ShouldBe(first.BlobName);
        second.Hash.ShouldBe(first.Hash);
        second.Size.ShouldBe(first.Size);
        second.MimeType.ShouldBe(first.MimeType);
        first.OwnsBlob().ShouldBeTrue();
        second.OwnsBlob().ShouldBeFalse();
        (await ReadAsync(second)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Not_Dedup_Across_Tenants()
    {
        var bytes = DriveTestFiles.Pdf("tenants " + Guid.NewGuid());

        FileDescriptor first, second;
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            first = await CreateAsync(DriveTestContainers.Files, "a.pdf", bytes);
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            second = await CreateAsync(DriveTestContainers.Files, "a.pdf", bytes);
        }

        second.BlobName.ShouldNotBe(first.BlobName);
        second.OwnsBlob().ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Should_Keep_A_Blob_While_Another_Descriptor_Uses_It()
    {
        var bytes = DriveTestFiles.Pdf("shared " + Guid.NewGuid());
        var writer = await CreateAsync(DriveTestContainers.Files, "writer.pdf", bytes);
        var sharer = await CreateAsync(DriveTestContainers.Files, "sharer.pdf", bytes);

        // The writer goes first: its BLOB stays for the sharer.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(writer));
        (await BlobExistsAsync(DriveTestContainers.Files, writer.BlobName)).ShouldBeTrue();
        (await ReadAsync(sharer)).ShouldBe(bytes);
        (await FindAsync(DriveTestContainers.Files, "writer.pdf")).ShouldBeNull();

        // The last user goes: the BLOB goes with it.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(sharer));
        (await BlobExistsAsync(DriveTestContainers.Files, writer.BlobName)).ShouldBeFalse();
    }

    /// <summary>
    /// A deleted writer gives up its dedup key, so the same bytes can be stored again once no live descriptor has
    /// them; the deleted row keeps its hash for the audit trail.
    /// </summary>
    [Fact]
    public async Task Should_Store_The_Same_Bytes_Again_After_They_Were_Deleted()
    {
        var bytes = DriveTestFiles.Pdf("again " + Guid.NewGuid());
        var first = await CreateAsync(DriveTestContainers.Files, "first.pdf", bytes);
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));

        var second = await CreateAsync(DriveTestContainers.Files, "again.pdf", bytes);

        second.OwnsBlob().ShouldBeTrue();
        second.BlobName.ShouldNotBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var deleted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(first.Id));
            deleted.IsDeleted.ShouldBeTrue();
            deleted.Hash.ShouldBe(first.Hash);
            deleted.OwnsBlob().ShouldBeFalse();
        }
    }

    /// <summary>
    /// Two simultaneous uploads of the same bytes can both miss the dedup lookup. The unique dedup key lets only one of
    /// them keep its BLOB: the loser's insert fails and its BLOB is removed, and a retry shares the winner's.
    /// </summary>
    [Fact]
    public async Task The_Loser_Of_A_Dedup_Race_Should_Fail_And_Leave_No_Blob()
    {
        var bytes = DriveTestFiles.Pdf("race " + Guid.NewGuid());
        var winner = await CreateAsync(DriveTestContainers.Files, "winner.pdf", bytes);

        var racing = GetRequiredService<RacingFileDescriptorManager>();
        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await racing.CreateAsync(DriveTestContainers.Files, "loser.pdf", stream);
        }));

        racing.GeneratedBlobNames.Count.ShouldBe(1);
        (await BlobExistsAsync(DriveTestContainers.Files, racing.GeneratedBlobNames[0])).ShouldBeFalse();
        (await FindAsync(DriveTestContainers.Files, "loser.pdf")).ShouldBeNull();

        var retry = await CreateAsync(DriveTestContainers.Files, "loser.pdf", bytes);
        retry.BlobName.ShouldBe(winner.BlobName);
        (await ReadAsync(winner)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Refuse_Content_Its_Extension_Contradicts_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(DriveTestContainers.Files, "picture.png", DriveTestFiles.Pdf("not a picture")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        (await FindAsync(DriveTestContainers.Files, "picture.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Run_The_Containers_Contributors()
    {
        var tooLarge = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(DriveTestContainers.Limited, "large.pdf", DriveTestFiles.Pdf(new string('x', (int)DriveTestContainers.LimitedMaxSizeInBytes))));
        tooLarge.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);

        var notAllowed = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(DriveTestContainers.Limited, "notes.txt", DriveTestFiles.Text("not a pdf")));
        notAllowed.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await FindAsync(DriveTestContainers.Limited, "notes.txt")).ShouldBeNull();

        var allowed = await CreateAsync(DriveTestContainers.Limited, "small.pdf", DriveTestFiles.Pdf("small"));
        allowed.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task A_Name_Without_An_Extension_Is_Typed_From_The_Content()
    {
        var descriptor = await CreateAsync(DriveTestContainers.Files, "no-extension-" + Guid.NewGuid().ToString("N"), DriveTestFiles.Pdf("typed"));

        descriptor.MimeType.ShouldBe("application/pdf");
        descriptor.BlobName.ShouldEndWith(".pdf");
    }

    private Task<FileDescriptor> CreateAsync(string containerName, string name, byte[] bytes, bool overrideExisting = false)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, name, stream, overrideExisting);
        });
    }

    private Task<FileDescriptor?> FindAsync(string containerName, string name)
    {
        return WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(containerName, name));
    }

    private Task<byte[]> ReadAsync(FileDescriptor descriptor)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            stream.ShouldNotBeNull();
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            return memory.ToArray();
        });
    }

    private Task<bool> BlobExistsAsync(string containerName, string blobName)
    {
        return _blobContainerFactory.Create(containerName).ExistsAsync(blobName);
    }
}
