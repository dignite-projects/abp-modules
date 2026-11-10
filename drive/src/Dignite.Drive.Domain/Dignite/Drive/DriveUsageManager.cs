using System.Threading;
using System.Threading.Tasks;
using Dignite.Drive.Features;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Features;

namespace Dignite.Drive;

/// <summary>
/// The current tenant's allocation: the <see cref="DriveFeatures"/> checked against what it stores. Usage is computed
/// from the descriptor table - each stored BLOB counted once, however many descriptors share it, deleted descriptors
/// not at all - per tenant only, across every Drive container.
/// <para>
/// The check is a soft limit: it reads the usage, then the caller stores, so two simultaneous uploads can both pass it
/// and together exceed the allocation by up to one upload each.
/// </para>
/// </summary>
public class DriveUsageManager : DomainService
{
    protected const long BytesPerMegabyte = 1024L * 1024L;

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IFeatureChecker FeatureChecker { get; }

    public DriveUsageManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IFeatureChecker featureChecker)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        FeatureChecker = featureChecker;
    }

    /// <summary>The bytes the current tenant stores.</summary>
    public virtual Task<long> GetUsedBytesAsync(CancellationToken cancellationToken = default)
    {
        return FileDescriptorRepository.GetUsedStorageSizeAsync(cancellationToken);
    }

    /// <summary>
    /// The current tenant's allocation in bytes, or <c>null</c> when <see cref="DriveFeatures.StorageSize"/> sets no
    /// limit.
    /// </summary>
    public virtual async Task<long?> GetStorageSizeAsync()
    {
        var megabytes = await FeatureChecker.GetAsync(DriveFeatures.StorageSize, 0);
        return megabytes > 0 ? megabytes * BytesPerMegabyte : null;
    }

    /// <summary>Fails with <see cref="DriveErrorCodes.DriveDisabled"/> when the tenant's Drive is off.</summary>
    public virtual async Task CheckEnabledAsync()
    {
        if (!await FeatureChecker.IsEnabledAsync(DriveFeatures.Enable))
        {
            throw new BusinessException(DriveErrorCodes.DriveDisabled);
        }
    }

    /// <summary>
    /// Fails with <see cref="DriveErrorCodes.StorageQuotaExceeded"/> when storing <paramref name="requiredBytes"/> more
    /// would take the tenant past its allocation.
    /// </summary>
    public virtual async Task CheckCanStoreAsync(long requiredBytes, CancellationToken cancellationToken = default)
    {
        var storageSize = await GetStorageSizeAsync();
        if (storageSize == null)
        {
            return;
        }

        var usedBytes = await GetUsedBytesAsync(cancellationToken);
        if (usedBytes + requiredBytes > storageSize.Value)
        {
            throw new BusinessException(DriveErrorCodes.StorageQuotaExceeded)
                .WithData("StorageSize", storageSize.Value / BytesPerMegabyte)
                .WithData("UsedBytes", usedBytes)
                .WithData("RequiredBytes", requiredBytes);
        }
    }
}
