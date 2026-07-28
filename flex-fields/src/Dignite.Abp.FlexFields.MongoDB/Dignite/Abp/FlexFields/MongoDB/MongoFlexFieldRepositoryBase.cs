using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="IFlexFieldRepository{TField}"/> for a downstream's own
/// <see cref="IFlexField"/> entity - the counterpart of the EF Core provider's
/// <c>EfCoreFlexFieldRepositoryBase&lt;TDbContext, TField&gt;</c>. Abstract and generic over the downstream's
/// context interface and field entity type, so the kernel never names a concrete context or collection.
/// <para>
/// Implements <see cref="ITransientDependency"/> directly, which is enough for a downstream's concrete
/// subclass to resolve as itself and as the standard ABP repository interfaces.
/// </para>
/// <para>
/// It is <b>not</b> enough for <see cref="IFlexFieldRepository{TField}"/> itself: that match requires the
/// concrete class name to end with "FlexFieldRepository" specifically. Most downstream repository classes
/// won't happen to be named that way, so register the interface explicitly in the downstream's own module:
/// <code>
/// context.Services.AddTransient&lt;IFlexFieldRepository&lt;Field&gt;, MongoFieldRepository&gt;();
/// </code>
/// </para>
/// </summary>
/// <typeparam name="TMongoDbContext">The downstream's own MongoDB context interface.</typeparam>
/// <typeparam name="TField">The downstream's own field-definition entity type.</typeparam>
public abstract class MongoFlexFieldRepositoryBase<TMongoDbContext, TField>
    : MongoDbRepository<TMongoDbContext, TField, Guid>, IFlexFieldRepository<TField>, ITransientDependency
    where TMongoDbContext : IAbpMongoDbContext
    where TField : class, IFlexField
{
    protected MongoFlexFieldRepositoryBase(IMongoDbContextProvider<TMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<TField?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .FirstOrDefaultAsync(f => f.Name == name, token);
    }

    public virtual async Task<List<TField>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(token);
    }

    public virtual async Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(token))
            .AnyAsync(f => f.Name == name && (excludedId == null || f.Id != excludedId), token);
    }
}
