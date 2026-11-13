using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

public class MongoDirectoryDescriptorRepository
    : MongoDbRepository<IBlobManagementMongoDbContext, DirectoryDescriptor, Guid>, IDirectoryDescriptorRepository
{
    public MongoDirectoryDescriptorRepository(IMongoDbContextProvider<IBlobManagementMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> NameKeyExistsAsync(string nameKey, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token)).AnyAsync(d => d.NameKey == nameKey, token);
    }

    public virtual async Task<DirectoryDescriptor?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token)).FirstOrDefaultAsync(d => d.NameKey == nameKey, token);
    }

    public virtual async Task<List<DirectoryDescriptor>> GetChildrenAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Where(d => d.ContainerName == containerName && d.ParentId == parentId)
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Id)
            .ToListAsync(token);
    }

    public virtual async Task<int> GetMaxOrderAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var orders = await (await GetQueryableAsync(token))
            .Where(d => d.ContainerName == containerName && d.ParentId == parentId)
            .OrderByDescending(d => d.Order)
            .Select(d => d.Order)
            .Take(1)
            .ToListAsync(token);
        return orders.Count == 0 ? 0 : orders[0];
    }

    public virtual async Task<List<DirectoryDescriptor>> GetListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var idList = ids.Distinct().ToList();
        return await (await GetQueryableAsync(token))
            .Where(d => idList.Contains(d.Id))
            .ToListAsync(token);
    }

    public virtual async Task<List<DirectoryDescriptor>> GetDescendantsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        // The driver turns StartsWith into an anchored, escaped regular expression, which the Path index serves.
        var token = GetCancellationToken(cancellationToken);
        var prefix = path + DirectoryDescriptorConsts.PathSeparator;
        return await (await GetQueryableAsync(token))
            .Where(d => d.Path.StartsWith(prefix))
            .ToListAsync(token);
    }

    public virtual async Task<List<Guid>> GetIdsWithChildrenAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var parentIds = ids.Select(id => (Guid?)id).ToList();
        var children = await (await GetQueryableAsync(token))
            .Where(d => parentIds.Contains(d.ParentId))
            .Select(d => d.ParentId)
            .ToListAsync(token);
        return children.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
    }

    public virtual async Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token)).AnyAsync(d => d.ParentId == id, token);
    }
}
