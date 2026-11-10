using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Drive.EntityFrameworkCore;

public class EfCoreFileDescriptorRepository : EfCoreRepository<IDriveDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public EfCoreFileDescriptorRepository(IDbContextProvider<IDriveDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<FileDescriptor?> FindByNameAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.ContainerName == containerName && f.Name == name, GetCancellationToken(cancellationToken));
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

    public virtual async Task<bool> IsBlobInUseAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.ContainerName == containerName && f.BlobName == blobName, GetCancellationToken(cancellationToken));
    }
}
