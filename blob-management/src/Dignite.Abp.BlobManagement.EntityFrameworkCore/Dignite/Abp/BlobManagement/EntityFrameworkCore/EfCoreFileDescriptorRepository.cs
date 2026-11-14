using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

public class EfCoreFileDescriptorRepository : EfCoreRepository<IBlobManagementDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public EfCoreFileDescriptorRepository(IDbContextProvider<IBlobManagementDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<FileDescriptor?> FindByNameAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                f => f.ContainerName == containerName && f.Name == name && f.NameKey != null,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetUsedStorageSizeAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .SumAsync(f => f.Size, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<BlobManagementContainerUsage>> GetContainerUsagesAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .GroupBy(f => f.ContainerName)
            .Select(g => new BlobManagementContainerUsage
            {
                ContainerName = g.Key,
                StoredBytes = g.Sum(f => f.Size),
                FileCount = g.LongCount()
            })
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<BlobManagementCreatorUsage>> GetCreatorUsagesAsync(
        string? containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .WhereIf(containerName != null, f => f.ContainerName == containerName)
            .GroupBy(f => f.CreatorId)
            .Select(g => new BlobManagementCreatorUsage
            {
                CreatorId = g.Key,
                StoredBytes = g.Sum(f => f.Size),
                FileCount = g.LongCount()
            })
            // The id ends the order, so two pages never share or miss a user.
            .OrderByDescending(u => u.StoredBytes)
            .ThenBy(u => u.CreatorId)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCreatorCountAsync(string? containerName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .WhereIf(containerName != null, f => f.ContainerName == containerName)
            .Select(f => f.CreatorId)
            .Distinct()
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<BlobManagementClientUsage>> GetClientUsagesAsync(
        string? containerName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .WhereIf(containerName != null, f => f.ContainerName == containerName)
            .GroupBy(f => f.ClientId)
            .Select(g => new BlobManagementClientUsage
            {
                ClientId = g.Key,
                StoredBytes = g.Sum(f => f.Size),
                FileCount = g.LongCount()
            })
            .OrderByDescending(u => u.StoredBytes)
            .ThenBy(u => u.ClientId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindPublicByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                f => f.ContainerName == containerName && f.BlobName == blobName && f.IsPublic && !f.IsPending && f.NameKey == null,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FileDescriptor>> GetListAsync(
        string containerName,
        Guid? directoryId,
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        return await (await GetListQueryAsync(containerName, directoryId, filter))
            .OrderBy(FileDescriptorSorting.Normalize(sorting))
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(
        string containerName,
        Guid? directoryId,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetListQueryAsync(containerName, directoryId, filter))
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> AnyInDirectoryAsync(Guid directoryId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.DirectoryId == directoryId, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FileDescriptor>> GetExpiredPendingListAsync(
        DateTime createdBefore,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(f => f.IsPending && f.CreationTime < createdBefore)
            .OrderBy(f => f.CreationTime)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FileDescriptor>> GetUnfiledListAsync(
        string containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(f => f.ContainerName == containerName && f.DirectoryId == null && f.NameKey == null && !f.IsPending)
            .OrderBy(f => f.Id)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual async Task<IQueryable<FileDescriptor>> GetListQueryAsync(
        string containerName,
        Guid? directoryId,
        string? filter)
    {
        // Upper-cased on both sides: a search finds a name whatever its case on every provider, not only where the
        // database's collation happens to ignore it.
        var term = filter.IsNullOrWhiteSpace() ? null : filter!.Trim().ToUpperInvariant();
        return (await GetDbSetAsync())
            .Where(f => f.ContainerName == containerName && f.DirectoryId == directoryId && !f.IsPending)
            .WhereIf(term != null, f => f.Name.ToUpper().Contains(term!));
    }
}
