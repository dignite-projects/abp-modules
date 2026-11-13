using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

public class MongoFileDescriptorRepository : MongoDbRepository<IBlobManagementMongoDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public MongoFileDescriptorRepository(IMongoDbContextProvider<IBlobManagementMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<FileDescriptor?> FindByNameAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(
            f => f.ContainerName == containerName && f.Name == name && f.NameKey != null,
            includeDetails: false,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<FileDescriptor?> FindByHashAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(
            f => f.ContainerName == containerName && f.Hash == hash,
            includeDetails: false,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetUsedStorageSizeAsync(CancellationToken cancellationToken = default)
    {
        // Descriptors sharing a BLOB carry the same size, so the distinct (container, BLOB, size) rows are the BLOBs.
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Select(f => new { f.ContainerName, f.BlobName, f.Size })
            .Distinct()
            .SumAsync(b => b.Size, token);
    }

    public virtual async Task<FileDescriptor?> FindByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(
            f => f.ContainerName == containerName && f.BlobName == blobName,
            includeDetails: false,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> IsBlobInUseAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .AnyAsync(f => f.ContainerName == containerName && f.BlobName == blobName, token);
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
        var token = GetCancellationToken(cancellationToken);
        return await (await GetListQueryAsync(containerName, directoryId, filter, token))
            .OrderBy(FileDescriptorSorting.Normalize(sorting))
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(token);
    }

    public virtual async Task<long> GetCountAsync(
        string containerName,
        Guid? directoryId,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetListQueryAsync(containerName, directoryId, filter, token)).LongCountAsync(token);
    }

    public virtual async Task<bool> AnyInDirectoryAsync(Guid directoryId, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        Guid? directory = directoryId;
        return await (await GetQueryableAsync(token)).AnyAsync(f => f.DirectoryId == directory, token);
    }

    public virtual async Task<List<FileDescriptor>> GetUnfiledListAsync(
        string containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Where(f => f.ContainerName == containerName && f.DirectoryId == null && f.NameKey == null)
            .OrderBy(f => f.Id)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(token);
    }

    protected virtual async Task<IQueryable<FileDescriptor>> GetListQueryAsync(
        string containerName,
        Guid? directoryId,
        string? filter,
        CancellationToken cancellationToken)
    {
        // Upper-cased on both sides, as on EF Core: a search finds a name whatever its case.
        var term = filter.IsNullOrWhiteSpace() ? null : filter!.Trim().ToUpperInvariant();
        return (await GetQueryableAsync(cancellationToken))
            .Where(f => f.ContainerName == containerName && f.DirectoryId == directoryId)
            .WhereIf(term != null, f => f.Name.ToUpper().Contains(term!));
    }
}
