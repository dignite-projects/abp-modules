﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.FileExplorer.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.FileExplorer.Files;

public class EfCoreFileDescriptorRepository : EfCoreRepository<IFileExplorerDbContext, FileDescriptor, Guid>, IFileDescriptorRepository
{
    public EfCoreFileDescriptorRepository(
        IDbContextProvider<IFileExplorerDbContext> dbContextProvider
        )
        : base(dbContextProvider)
    {
    }

    public async Task<bool> BlobNameExistsAsync(string containerName, string blobName, Guid? ignoredId = null, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
                   .WhereIf(ignoredId != null, ct => ct.Id != ignoredId)
                   .AnyAsync(b => b.ContainerName == containerName && b.BlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public async Task<FileDescriptor> FindAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
                   .FirstOrDefaultAsync(b => b.ContainerName == containerName && b.BlobName == blobName, GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetCountAsync(string containerName, string filter = null, string entityType = null, string entityId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken = GetCancellationToken(cancellationToken);

        var query = await GetListQueryAsync(
            containerName, filter, entityType, entityId
        );

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<FileDescriptor>> GetListAsync(
        string containerName,
        string filter = null,
        string entityTypeFullName = null,
        string entityId = null,
        string sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = GetCancellationToken(cancellationToken);

        var query = await GetListQueryAsync(
            containerName, filter, entityTypeFullName, entityId
        );

        return await query.OrderBy(sorting.IsNullOrWhiteSpace() ? $"{nameof(FileDescriptor.CreationTime)} desc" : sorting)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<FileDescriptor>> GetListQueryAsync(
        string containerName,
        string filter = null,
        string entityTypeFullName = null,
        string entityId = null)
    {
        return (await GetDbSetAsync()).AsNoTracking()
            .WhereIf(!containerName.IsNullOrWhiteSpace(), fd => fd.ContainerName == containerName)
            .WhereIf(!filter.IsNullOrWhiteSpace(), fd => fd.Name.Contains(filter))
            .WhereIf(!entityTypeFullName.IsNullOrWhiteSpace(), fd => fd.EntityTypeFullName == entityTypeFullName)
            .WhereIf(!entityId.IsNullOrWhiteSpace(), fd => fd.EntityId == entityId);
    }
}