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
/// A downstream subclass supplies only the two things the kernel cannot know: the name of its own
/// foreign-key property, and how to construct one of its own index rows.
/// </para>
/// </summary>
/// <typeparam name="TDbContext">The downstream's own DbContext interface.</typeparam>
/// <typeparam name="TEntity">The host entity type.</typeparam>
/// <typeparam name="TIndex">The downstream's <see cref="FlexFieldIndexBase{TEntity}"/> subclass.</typeparam>
public abstract class EfCoreFlexFieldIndexManagerBase<TDbContext, TEntity, TIndex> : IFlexFieldIndexManager<TEntity>
    where TDbContext : IEfCoreDbContext
    where TEntity : class, IHasFlexFields, IEntity<Guid>
    where TIndex : class, IFlexFieldIndex
{
    protected IDbContextProvider<TDbContext> DbContextProvider { get; }

    protected IFlexFieldProvider<TEntity> FlexFieldProvider { get; }

    protected IFieldTypeResolver FieldTypeResolver { get; }

    /// <summary>
    /// How many host entities <see cref="RebuildAsync"/> processes per flush. Bounds memory on a full
    /// rebuild; override to trade round trips against change-tracker size.
    /// </summary>
    protected virtual int RebuildPageSize => 100;

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
    {
        DbContextProvider = dbContextProvider;
        FlexFieldProvider = flexFieldProvider;
        FieldTypeResolver = fieldTypeResolver;
    }

    /// <summary>
    /// Deliberately does not call <c>SaveChanges</c>: the derived rows must commit with the value bag
    /// they came from, so they ride the caller's ambient unit of work.
    /// </summary>
    public virtual async Task SynchronizeAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();
        await ReplaceRowsAsync(dbContext, entity, cancellationToken);
    }

    /// <summary>
    /// Unlike <see cref="SynchronizeAsync"/> this does flush per page - the point of paging is to keep a
    /// full rebuild's memory bounded, which accumulating every change until the outer unit of work
    /// completes would defeat.
    /// </summary>
    public virtual async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        var skipCount = 0;

        while (true)
        {
            var page = await FlexFieldProvider.GetPagedEntitiesAsync(skipCount, RebuildPageSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            var dbContext = await DbContextProvider.GetDbContextAsync();
            foreach (var entity in page)
            {
                await ReplaceRowsAsync(dbContext, entity, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (page.Count < RebuildPageSize)
            {
                break;
            }

            skipCount += RebuildPageSize;
        }
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

    protected virtual async Task<List<TIndex>> ProjectAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var rows = new List<TIndex>();
        var fields = await FlexFieldProvider.GetFlexFieldsAsync(entity, cancellationToken);

        // Filtered here rather than trusting every IFieldType to honour it - only searchable fields are
        // ever indexed, and that is this manager's policy, not a field type's.
        foreach (var field in fields.Where(f => f.Searchable))
        {
            var fieldType = FieldTypeResolver.Get(field.FieldTypeName);
            if (fieldType.IndexValueType == null)
            {
                // Not indexable (e.g. RichText, Matrix) - GetSearchableValues would yield nothing anyway,
                // but there is no typed slot to pair its values with, so this manager's policy is to skip
                // it explicitly rather than let a field type's own emptiness decide.
                continue;
            }

            foreach (var value in fieldType.GetSearchableValues(field))
            {
                rows.Add(CreateIndexRow(
                    entity.Id,
                    field.FieldId,
                    FlexFieldIndexValue.Create(fieldType.IndexValueType.Value, value)));
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
