using System;
using System.Collections.Generic;
using System.Linq;
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
            f => f.ContainerName == containerName && f.Name == name,
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
}
