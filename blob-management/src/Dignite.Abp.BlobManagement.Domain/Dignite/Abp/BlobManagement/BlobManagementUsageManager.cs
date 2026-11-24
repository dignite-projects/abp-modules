using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The current tenant's allocation: the <see cref="BlobManagementFeatures"/> checked against what it stores. Usage is computed
/// from the descriptor table - the sizes of the descriptors, each with a BLOB of its own - per tenant only, across
/// every Blob Management container. A soft-deleted file keeps its bytes until its business system deletes it for good,
/// so it is counted: what the allocation measures is what is stored.
/// <para>
/// The check is a soft limit: it reads the usage, then the caller stores, so two simultaneous uploads can both pass it
/// and together exceed the allocation by up to one upload each.
/// </para>
/// </summary>
public class BlobManagementUsageManager : DomainService
{
    protected const long BytesPerMegabyte = 1024L * 1024L;

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IFeatureChecker FeatureChecker { get; }

    protected IDataFilter DataFilter => LazyServiceProvider.LazyGetRequiredService<IDataFilter>();

    public BlobManagementUsageManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IFeatureChecker featureChecker)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        FeatureChecker = featureChecker;
    }

    /// <summary>The bytes the current tenant stores.</summary>
    public virtual Task<long> GetUsedBytesAsync(CancellationToken cancellationToken = default)
    {
        return ReadStoredAsync(() => FileDescriptorRepository.GetUsedStorageSizeAsync(cancellationToken));
    }

    /// <summary>
    /// The current tenant's allocation in bytes, or <c>null</c> when <see cref="BlobManagementFeatures.StorageSize"/> sets no
    /// limit.
    /// </summary>
    public virtual async Task<long?> GetStorageSizeAsync()
    {
        var megabytes = await FeatureChecker.GetAsync(BlobManagementFeatures.StorageSize, 0);
        return megabytes > 0 ? megabytes * BytesPerMegabyte : null;
    }

    /// <summary>What each container of the current tenant stores.</summary>
    public virtual Task<List<BlobManagementContainerUsage>> GetContainerUsagesAsync(CancellationToken cancellationToken = default)
    {
        return ReadStoredAsync(() => FileDescriptorRepository.GetContainerUsagesAsync(cancellationToken));
    }

    /// <summary>What is counted for each user, the largest first, a page at a time - in one container, or in all.</summary>
    public virtual Task<List<BlobManagementCreatorUsage>> GetCreatorUsagesAsync(
        string? containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return ReadStoredAsync(() =>
            FileDescriptorRepository.GetCreatorUsagesAsync(containerName, skipCount, maxResultCount, cancellationToken));
    }

    /// <summary>How many rows <see cref="GetCreatorUsagesAsync"/> finds without paging.</summary>
    public virtual Task<long> GetCreatorCountAsync(string? containerName, CancellationToken cancellationToken = default)
    {
        return ReadStoredAsync(() => FileDescriptorRepository.GetCreatorCountAsync(containerName, cancellationToken));
    }

    /// <summary>What each client stored, the largest first - in one container, or in all.</summary>
    public virtual Task<List<BlobManagementClientUsage>> GetClientUsagesAsync(
        string? containerName,
        CancellationToken cancellationToken = default)
    {
        return ReadStoredAsync(() => FileDescriptorRepository.GetClientUsagesAsync(containerName, cancellationToken));
    }

    /// <summary>
    /// What each tenant of the deployment stores, and the host itself, the largest first, a page at a time - across
    /// tenants, with ABP's multi-tenant filter disabled around the read. The caller authorizes it for the host.
    /// </summary>
    public virtual async Task<List<BlobManagementTenantUsage>> GetTenantUsagesAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            return await ReadStoredAsync(() =>
                FileDescriptorRepository.GetTenantUsagesAsync(skipCount, maxResultCount, cancellationToken));
        }
    }

    /// <summary>How many rows <see cref="GetTenantUsagesAsync"/> finds without paging.</summary>
    public virtual async Task<long> GetTenantCountAsync(CancellationToken cancellationToken = default)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            return await ReadStoredAsync(() => FileDescriptorRepository.GetTenantCountAsync(cancellationToken));
        }
    }

    /// <summary>
    /// The allocation in bytes of <paramref name="tenantId"/> - <c>null</c> for the host - or <c>null</c> when it has no
    /// limit: <see cref="GetStorageSizeAsync"/>, read in that tenant.
    /// </summary>
    public virtual async Task<long?> GetStorageSizeAsync(Guid? tenantId)
    {
        using (CurrentTenant.Change(tenantId))
        {
            return await GetStorageSizeAsync();
        }
    }

    /// <summary>Fails with <see cref="BlobManagementErrorCodes.BlobManagementDisabled"/> when the tenant's Blob Management is off.</summary>
    public virtual async Task CheckEnabledAsync()
    {
        if (!await FeatureChecker.IsEnabledAsync(BlobManagementFeatures.Enable))
        {
            throw new BusinessException(BlobManagementErrorCodes.BlobManagementDisabled);
        }
    }

    /// <summary>
    /// Fails with <see cref="BlobManagementErrorCodes.StorageQuotaExceeded"/> when storing <paramref name="requiredBytes"/>
    /// more and then freeing <paramref name="releasedBytes"/> - the bytes of a file the store replaces, still counted
    /// while this runs - would take the tenant past its allocation: the net change is what is checked.
    /// </summary>
    public virtual async Task CheckCanStoreAsync(long requiredBytes, long releasedBytes = 0, CancellationToken cancellationToken = default)
    {
        var storageSize = await GetStorageSizeAsync();
        if (storageSize == null)
        {
            return;
        }

        var usedBytes = await GetUsedBytesAsync(cancellationToken);
        if (usedBytes - releasedBytes + requiredBytes > storageSize.Value)
        {
            throw new BusinessException(BlobManagementErrorCodes.StorageQuotaExceeded)
                .WithData("StorageSize", storageSize.Value / BytesPerMegabyte)
                .WithData("UsedBytes", usedBytes)
                .WithData("RequiredBytes", requiredBytes);
        }
    }

    /// <summary>
    /// Runs a usage query over every descriptor that holds bytes, soft-deleted ones included: a file deleted but not for
    /// good keeps its BLOB, and what is stored is what usage measures.
    /// </summary>
    protected virtual async Task<T> ReadStoredAsync<T>(Func<Task<T>> read)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            return await read();
        }
    }
}
