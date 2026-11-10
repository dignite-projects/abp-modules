using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Drive.Features;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The tenant allocation (<see cref="DriveFeatures"/>) as <see cref="FileDescriptorManager"/> enforces it, and the usage
/// <see cref="DriveUsageManager"/> computes from the descriptor table.
/// </summary>
public abstract class DriveUsageManager_Tests<TStartupModule> : DriveDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const int Kilobyte = 1024;

    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly DriveUsageManager _usageManager;
    private readonly FakeFeatureStore _featureStore;
    private readonly ICurrentTenant _currentTenant;

    protected DriveUsageManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _usageManager = GetRequiredService<DriveUsageManager>();
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

            var first = await CreateAsync(DriveTestContainers.Files, "first.pdf", shared);
            await CreateAsync(DriveTestContainers.Files, "first-again.pdf", shared);
            var second = await CreateAsync(DriveTestContainers.Limited, "second.pdf", OfSize(Kilobyte / 2, "small"));
            var third = await CreateAsync(DriveTestContainers.Files, "third.pdf", other);

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
    public async Task A_Tenant_Whose_Drive_Is_Off_Should_Store_Nothing()
    {
        var tenantId = Guid.NewGuid();
        _featureStore.SetForTenant(tenantId, DriveFeatures.Enable, "false");

        using (_currentTenant.Change(tenantId))
        {
            var exception = await Should.ThrowAsync<BusinessException>(() =>
                CreateAsync(DriveTestContainers.Files, "off.pdf", DriveTestFiles.Pdf("off")));

            exception.Code.ShouldBe(DriveErrorCodes.DriveDisabled);
            (await UsedBytesAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Should_Refuse_Content_Beyond_The_Storage_Size_But_Not_A_Dedup()
    {
        var tenantId = Guid.NewGuid();
        _featureStore.SetForTenant(tenantId, DriveFeatures.StorageSize, "1");

        using (_currentTenant.Change(tenantId))
        {
            var first = OfSize(600 * Kilobyte, "first");
            await CreateAsync(DriveTestContainers.Files, "first.pdf", first);

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                CreateAsync(DriveTestContainers.Files, "second.pdf", OfSize(600 * Kilobyte, "second")));

            exception.Code.ShouldBe(DriveErrorCodes.StorageQuotaExceeded);
            exception.Data["StorageSize"].ShouldBe(1L);
            exception.Data["UsedBytes"].ShouldBe((long)first.Length);
            (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(DriveTestContainers.Files, "second.pdf"))).ShouldBeNull();
            (await UsedBytesAsync()).ShouldBe(first.Length);

            // The same bytes again add nothing, so they fit.
            var copy = await CreateAsync(DriveTestContainers.Files, "first-copy.pdf", first);
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
        _featureStore.SetForTenant(tenantId, DriveFeatures.StorageSize, "5");
        using (_currentTenant.Change(tenantId))
        {
            (await _usageManager.GetStorageSizeAsync()).ShouldBe(5L * 1024 * 1024);
        }
    }

    /// <summary>A PDF of exactly <paramref name="length"/> bytes, distinct per <paramref name="seed"/>.</summary>
    private static byte[] OfSize(int length, string seed)
    {
        var shell = DriveTestFiles.Pdf(seed).Length;
        return DriveTestFiles.Pdf(seed + new string('x', length - shell));
    }

    private Task<FileDescriptor> CreateAsync(string containerName, string name, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, name, stream);
        });
    }

    private Task<long> UsedBytesAsync()
    {
        return WithUnitOfWorkAsync(() => _usageManager.GetUsedBytesAsync());
    }
}
