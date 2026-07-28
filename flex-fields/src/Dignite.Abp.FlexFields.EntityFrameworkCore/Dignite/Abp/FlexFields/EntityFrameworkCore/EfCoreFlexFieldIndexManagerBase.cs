using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IFlexFieldIndexManager{TEntity}"/>: projects a host entity's
/// searchable fields and writes them to the downstream's own index table. Abstract and generic over the
/// downstream's DbContext interface, host entity and index row type - the same shape as ABP's
/// <c>EfCoreUserRepositoryBase&lt;TDbContext, TUser&gt;</c>, so the kernel never names a concrete
/// DbContext or table.
/// <para>
/// Paging, termination and which field values are eligible to be indexed all come from
/// <see cref="FlexFieldIndexManagerBase{TEntity}"/> - none of that is relational. What is relational, and so
/// lives here, is the pivot table: a row per decomposed value, in a typed column chosen by the field type's
/// <see cref="IFieldType.IndexValueType"/>.
/// </para>
/// <para>
/// A downstream subclass supplies only the two things the kernel cannot know: the name of its own
/// foreign-key property, and how to construct one of its own index rows.
/// </para>
/// </summary>
/// <typeparam name="TDbContext">The downstream's own DbContext interface.</typeparam>
/// <typeparam name="TEntity">The host entity type.</typeparam>
/// <typeparam name="TIndex">The downstream's <see cref="FlexFieldIndexBase{TEntity}"/> subclass.</typeparam>
public abstract class EfCoreFlexFieldIndexManagerBase<TDbContext, TEntity, TIndex> : FlexFieldIndexManagerBase<TEntity>
    where TDbContext : IEfCoreDbContext
    where TEntity : class, IHasFlexFields, IEntity<Guid>
    where TIndex : class, IFlexFieldIndex
{
    protected IDbContextProvider<TDbContext> DbContextProvider { get; }

    /// <summary>
    /// Name of the downstream's own foreign-key property on <typeparamref name="TIndex"/> - the kernel
    /// maps no relationship, so it has to be told. Supply it with <c>nameof</c>.
    /// </summary>
    protected abstract string EntityIdPropertyName { get; }

    /// <summary>
    /// Builds one of the downstream's index rows. The downstream owns the row type (and its foreign key),
    /// so only it can construct one.
    /// </summary>
    protected abstract TIndex CreateIndexRow(Guid entityId, Guid fieldId, FlexFieldIndexValue value);

    protected EfCoreFlexFieldIndexManagerBase(
        IDbContextProvider<TDbContext> dbContextProvider,
        IFlexFieldProvider<TEntity> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver)
        : base(flexFieldProvider, fieldTypeResolver)
    {
        DbContextProvider = dbContextProvider;
    }

    /// <summary>
    /// Always reports a write: this provider replaces an entity's rows outright rather than diffing, so
    /// there is no cheaper "nothing changed" path to take.
    /// </summary>
    protected override async Task<bool> SynchronizeCoreAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();
        await ReplaceRowsAsync(dbContext, entity, cancellationToken);
        return true;
    }

    protected override async Task FlushAsync(CancellationToken cancellationToken)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Replace-all for one entity: drops whatever rows it currently has and re-adds its projection, so
    /// callers never diff old against new. Values that stopped being searchable simply produce no rows.
    /// </summary>
    protected virtual async Task ReplaceRowsAsync(
        TDbContext dbContext,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        var indexSet = dbContext.Set<TIndex>();
        var entityId = entity.Id;

        foreach (var existingRow in await FilterByEntity(indexSet, entityId).ToListAsync(cancellationToken))
        {
            indexSet.Remove(existingRow);
        }

        foreach (var row in await ProjectAsync(entity, cancellationToken))
        {
            await indexSet.AddAsync(row, cancellationToken);
        }
    }

    /// <summary>
    /// Decomposes each eligible value with <see cref="IFieldType.GetSearchableValues"/> - one row per value,
    /// so a multi-select becomes several - and types each one into a slot. The decomposition is what a
    /// pivot table needs and a document store does not, which is why it happens here rather than in the
    /// shared base.
    /// </summary>
    protected virtual async Task<List<TIndex>> ProjectAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var rows = new List<TIndex>();

        foreach (var indexable in await GetIndexableFieldsAsync(entity, cancellationToken))
        {
            foreach (var value in indexable.FieldType.GetSearchableValues(indexable.Value))
            {
                rows.Add(CreateIndexRow(
                    entity.Id,
                    indexable.Value.FieldId,
                    FlexFieldIndexValue.Create(indexable.IndexValueType, value)));
            }
        }

        return rows;
    }

    /// <summary>
    /// Filters to one host entity's rows through the downstream's foreign key. Uses
    /// <see cref="EF.Property{TProperty}"/> so no expression tree has to be hand-built, and the name is
    /// hoisted to a local because EF Core needs to fold it to a constant when translating.
    /// </summary>
    protected virtual IQueryable<TIndex> FilterByEntity(IQueryable<TIndex> query, Guid entityId)
    {
        var entityIdProperty = EntityIdPropertyName;
        return query.Where(x => EF.Property<Guid>(x, entityIdProperty) == entityId);
    }
}
