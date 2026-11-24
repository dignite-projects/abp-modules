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

    public virtual async Task<DirectoryDescriptor?> FindByNameAsync(
        string containerName,
        Guid? parentId,
        string name,
        CancellationToken cancellationToken = default)
    {
        // Read the siblings and compare here: the database's collation would decide the case otherwise.
        var siblings = await GetChildrenAsync(containerName, parentId, cancellationToken);
        return siblings.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
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
