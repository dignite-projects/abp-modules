using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

public class EfCoreDirectoryDescriptorRepository
    : EfCoreRepository<IBlobManagementDbContext, DirectoryDescriptor, Guid>, IDirectoryDescriptorRepository
{
    public EfCoreDirectoryDescriptorRepository(IDbContextProvider<IBlobManagementDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> NameKeyExistsAsync(string nameKey, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(d => d.NameKey == nameKey, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DirectoryDescriptor?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(d => d.NameKey == nameKey, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DirectoryDescriptor>> GetChildrenAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(d => d.ContainerName == containerName && d.ParentId == parentId)
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Id)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetMaxOrderAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(d => d.ContainerName == containerName && d.ParentId == parentId)
            .MaxAsync(d => (int?)d.Order, GetCancellationToken(cancellationToken)) ?? 0;
    }

    public virtual async Task<List<DirectoryDescriptor>> GetListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await (await GetDbSetAsync())
            .Where(d => idList.Contains(d.Id))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DirectoryDescriptor>> GetDescendantsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        // Ordinal: a path is hexadecimal digits and separators only, so neither case nor LIKE wildcards come into it.
        var prefix = path + DirectoryDescriptorConsts.PathSeparator;
        return await (await GetDbSetAsync())
            .Where(d => d.Path.StartsWith(prefix))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Guid>> GetIdsWithChildrenAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var parentIds = ids.Select(id => (Guid?)id).ToList();
        return await (await GetDbSetAsync())
            .Where(d => parentIds.Contains(d.ParentId))
            .Select(d => d.ParentId!.Value)
            .Distinct()
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(d => d.ParentId == id, GetCancellationToken(cancellationToken));
    }
}
