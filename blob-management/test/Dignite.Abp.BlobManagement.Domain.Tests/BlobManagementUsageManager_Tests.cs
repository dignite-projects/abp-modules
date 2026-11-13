using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The tenant allocation (<see cref="BlobManagementFeatures"/>) as <see cref="FileDescriptorManager"/> enforces it, and the usage
/// <see cref="BlobManagementUsageManager"/> and the descriptor repository compute from the descriptor table.
/// </summary>
public abstract class BlobManagementUsageManager_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const int Kilobyte = 1024;

    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly BlobManagementUsageManager _usageManager;
    private readonly IFileDescriptorRepository _fileRepository;
    private readonly FakeFeatureStore _featureStore;
    private readonly ICurrentTenant _currentTenant;

    protected BlobManagementUsageManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _usageManager = GetRequiredService<BlobManagementUsageManager>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
        _featureStore = GetRequiredService<FakeFeatureStore>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Usage_Should_Count_Each_Stored_Blob_Once_And_Only_Live_Ones()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var shared = OfSize(10 * Kilobyte, "shared");
            var other = OfSize(4 * Kilobyte, "other");

            var first = await CreateAsync(BlobManagementTestContainers.Files, "first.pdf", shared);
            await CreateAsync(BlobManagementTestContainers.Files, "first-again.pdf", shared);
            var second = await CreateAsync(BlobManagementTestContainers.Limited, "second.pdf", OfSize(Kilobyte / 2, "small"));
            var third = await CreateAsync(BlobManagementTestContainers.Files, "third.pdf", other);

            (await UsedBytesAsync()).ShouldBe(shared.Length + second.Size + other.Length);

            // Deleting one of two sharers frees nothing; deleting the only user of a BLOB frees it.
            await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));
            await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(third));
            (await UsedBytesAsync()).ShouldBe(shared.Length + second.Size);
        }

        // Usage is per tenant.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await UsedBytesAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Usage_Should_Be_Told_By_Container_By_User_And_By_Client()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var shared = OfSize(10 * Kilobyte, "shared");
            var other = OfSize(4 * Kilobyte, "other");

            await CreateAsync(BlobManagementTestContainers.Files, "alice.pdf", shared, "site", alice);
            await CreateAsync(BlobManagementTestContainers.Files, "bob.pdf", shared, "site", bob);
            await CreateAsync(BlobManagementTestContainers.Files, "bob-too.pdf", other, clientId: null, bob);
            var small = await CreateAsync(BlobManagementTestContainers.Limited, "nobody.pdf", OfSize(Kilobyte / 2, "small"));

            // A container stores each BLOB once, and the containers add up to the tenant.
            var containers = await WithUnitOfWorkAsync(() => _fileRepository.GetContainerUsagesAsync());
            containers.Count.ShouldBe(2);
            var files = containers.Single(c => c.ContainerName == BlobManagementTestContainers.Files);
            files.StoredBytes.ShouldBe(shared.Length + other.Length);
            files.FileCount.ShouldBe(3);
            var limited = containers.Single(c => c.ContainerName == BlobManagementTestContainers.Limited);
            limited.StoredBytes.ShouldBe(small.Size);
            limited.FileCount.ShouldBe(1);
            containers.Sum(c => c.StoredBytes).ShouldBe(await UsedBytesAsync());

            // A user is counted for every file of theirs, shared bytes too - the largest first, then the files of no user.
            var users = await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorUsagesAsync(null, 0, 10));
            users.Select(u => u.CreatorId).ShouldBe(new Guid?[] { bob, alice, null });
            users[0].UploadedBytes.ShouldBe(shared.Length + other.Length);
            users[0].FileCount.ShouldBe(2);
            users[1].UploadedBytes.ShouldBe(shared.Length);
            users[1].FileCount.ShouldBe(1);
            users[2].UploadedBytes.ShouldBe(small.Size);
            users.Sum(u => u.UploadedBytes).ShouldBeGreaterThan(await UsedBytesAsync());

            (await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorCountAsync(null))).ShouldBe(3);
            (await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorUsagesAsync(null, 1, 1)))
                .ShouldHaveSingleItem().CreatorId.ShouldBe(alice);

            // In one container.
            (await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorCountAsync(BlobManagementTestContainers.Limited))).ShouldBe(1);
            (await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorUsagesAsync(BlobManagementTestContainers.Limited, 0, 10)))
                .ShouldHaveSingleItem().CreatorId.ShouldBeNull();

            // A client likewise; the files stored in process are gathered under no client.
            var clients = await WithUnitOfWorkAsync(() => _fileRepository.GetClientUsagesAsync(null));
            clients.Select(c => c.ClientId).ShouldBe(new[] { "site", null });
            clients[0].UploadedBytes.ShouldBe(2L * shared.Length);
            clients[0].FileCount.ShouldBe(2);
            clients[1].UploadedBytes.ShouldBe(other.Length + small.Size);
            clients[1].FileCount.ShouldBe(2);

            (await WithUnitOfWorkAsync(() => _fileRepository.GetClientUsagesAsync(BlobManagementTestContainers.Limited)))
                .ShouldHaveSingleItem().ClientId.ShouldBeNull();
        }

        // Per tenant, as the tenant's usage is.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await WithUnitOfWorkAsync(() => _fileRepository.GetContainerUsagesAsync())).ShouldBeEmpty();
            (await WithUnitOfWorkAsync(() => _fileRepository.GetCreatorCountAsync(null))).ShouldBe(0);
            (await WithUnitOfWorkAsync(() => _fileRepository.GetClientUsagesAsync(null))).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task A_Tenant_Whose_BlobManagement_Is_Off_Should_Store_Nothing()
    {
        var tenantId = Guid.NewGuid();
        _featureStore.SetForTenant(tenantId, BlobManagementFeatures.Enable, "false");

        using (_currentTenant.Change(tenantId))
        {
            var exception = await Should.ThrowAsync<BusinessException>(() =>
                CreateAsync(BlobManagementTestContainers.Files, "off.pdf", BlobManagementTestFiles.Pdf("off")));

            exception.Code.ShouldBe(BlobManagementErrorCodes.BlobManagementDisabled);
            (await UsedBytesAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Should_Refuse_Content_Beyond_The_Storage_Size_But_Not_A_Dedup()
    {
        var tenantId = Guid.NewGuid();
        _featureStore.SetForTenant(tenantId, BlobManagementFeatures.StorageSize, "1");

        using (_currentTenant.Change(tenantId))
        {
            var first = OfSize(600 * Kilobyte, "first");
            await CreateAsync(BlobManagementTestContainers.Files, "first.pdf", first);

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                CreateAsync(BlobManagementTestContainers.Files, "second.pdf", OfSize(600 * Kilobyte, "second")));

            exception.Code.ShouldBe(BlobManagementErrorCodes.StorageQuotaExceeded);
            exception.Data["StorageSize"].ShouldBe(1L);
            exception.Data["UsedBytes"].ShouldBe((long)first.Length);
            (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(BlobManagementTestContainers.Files, "second.pdf"))).ShouldBeNull();
            (await UsedBytesAsync()).ShouldBe(first.Length);

            // The same bytes again add nothing, so they fit.
            var copy = await CreateAsync(BlobManagementTestContainers.Files, "first-copy.pdf", first);
            copy.OwnsBlob().ShouldBeFalse();
        }
    }

    [Fact]
    public async Task No_Storage_Size_Means_No_Limit()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await _usageManager.GetStorageSizeAsync()).ShouldBeNull();
            await _usageManager.CheckCanStoreAsync(long.MaxValue / 2);
        }

        var tenantId = Guid.NewGuid();
        _featureStore.SetForTenant(tenantId, BlobManagementFeatures.StorageSize, "5");
        using (_currentTenant.Change(tenantId))
        {
            (await _usageManager.GetStorageSizeAsync()).ShouldBe(5L * 1024 * 1024);
        }
    }

    /// <summary>A PDF of exactly <paramref name="length"/> bytes, distinct per <paramref name="seed"/>.</summary>
    private static byte[] OfSize(int length, string seed)
    {
        var shell = BlobManagementTestFiles.Pdf(seed).Length;
        return BlobManagementTestFiles.Pdf(seed + new string('x', length - shell));
    }

    private Task<FileDescriptor> CreateAsync(
        string containerName,
        string name,
        byte[] bytes,
        string? clientId = null,
        Guid? creatorId = null)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, name, stream, clientId: clientId, creatorId: creatorId);
        });
    }

    private Task<long> UsedBytesAsync()
    {
        return WithUnitOfWorkAsync(() => _usageManager.GetUsedBytesAsync());
    }
}
