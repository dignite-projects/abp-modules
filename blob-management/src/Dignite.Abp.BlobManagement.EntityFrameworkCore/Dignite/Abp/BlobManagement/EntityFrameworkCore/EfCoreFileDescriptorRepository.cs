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

    public virtual async Task<FileDescriptor?> FindByHashAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.ContainerName == containerName && f.Hash == hash, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetUsedStorageSizeAsync(CancellationToken cancellationToken = default)
    {
        // Descriptors sharing a BLOB carry the same size, so the distinct (container, BLOB, size) rows are the BLOBs.
        return await (await GetDbSetAsync())
            .Select(f => new { f.ContainerName, f.BlobName, f.Size })
            .Distinct()
            .SumAsync(b => b.Size, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.ContainerName == containerName && f.BlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> IsBlobInUseAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.ContainerName == containerName && f.BlobName == blobName, GetCancellationToken(cancellationToken));
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

    public virtual async Task<List<FileDescriptor>> GetUnfiledListAsync(
        string containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(f => f.ContainerName == containerName && f.DirectoryId == null && f.NameKey == null)
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
            .Where(f => f.ContainerName == containerName && f.DirectoryId == directoryId)
            .WhereIf(term != null, f => f.Name.ToUpper().Contains(term!));
    }
}
