using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IFlexFieldRepository{TField}"/> for a downstream's own
/// <see cref="IFlexField"/> entity - the same shape as ABP's own
/// <c>EfCoreUserRepositoryBase&lt;TDbContext, TUser&gt;</c>. Abstract and generic over the downstream's
/// DbContext interface and field entity type, so the kernel never names a concrete DbContext or table.
/// <para>
/// Implements <see cref="ITransientDependency"/> directly (the same self-registering posture as
/// <see cref="FieldTypeBase"/>), which is enough for a downstream's concrete subclass to resolve as
/// itself and as the standard ABP repository interfaces (<c>IRepository&lt;TField, Guid&gt;</c> etc. -
/// ABP's conventional registrar exposes any interface whose name, minus the leading "I", the concrete
/// class name ends with, and every one of those literally ends in "Repository").
/// </para>
/// <para>
/// It is <b>not</b> enough for <see cref="IFlexFieldRepository{TField}"/> itself: that match requires the
/// concrete class name to end with "FlexFieldRepository" specifically (the same reason ABP's own
/// <c>EfCoreIdentityUserRepository</c> has to be named to end with "IdentityUserRepository" to satisfy
/// <c>IIdentityUserRepository</c>). Most downstream repository classes won't happen to be named that way,
/// so register the interface explicitly in the downstream's own module:
/// <code>
/// context.Services.AddTransient&lt;IFlexFieldRepository&lt;Field&gt;, EfCoreFieldRepository&gt;();
/// </code>
/// </para>
/// </summary>
/// <typeparam name="TDbContext">The downstream's own DbContext interface.</typeparam>
/// <typeparam name="TField">The downstream's own field-definition entity type.</typeparam>
public abstract class EfCoreFlexFieldRepositoryBase<TDbContext, TField>
    : EfCoreRepository<TDbContext, TField, Guid>, IFlexFieldRepository<TField>, ITransientDependency
    where TDbContext : IEfCoreDbContext
    where TField : class, IFlexField
{
    protected EfCoreFlexFieldRepositoryBase(IDbContextProvider<TDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<TField?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(f => f.Name == name, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<TField>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(f => f.Name == name && (excludedId == null || f.Id != excludedId), GetCancellationToken(cancellationToken));
    }
}
