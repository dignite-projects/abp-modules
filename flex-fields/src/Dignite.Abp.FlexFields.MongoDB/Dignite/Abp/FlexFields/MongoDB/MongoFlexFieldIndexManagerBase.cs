using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="IFlexFieldIndexManager{TEntity}"/>. There is no pivot table and no
/// <c>FlexFieldIndexValue</c> equivalent here: the value bag is both the authoritative store and the thing
/// that gets indexed, which is the premise the two providers are separated on.
/// <para>
/// That is not the same as having nothing to do. A relational provider gets its types from the pivot table -
/// a value lands in a typed column or not at all - and having no such table means this provider has to meet
/// the same guarantee somewhere else. It meets it here: each eligible value is written back into the bag in
/// the form its <see cref="IFieldType.IndexValueType"/> names, so a native path filter has a well-typed value
/// to compare against. Without it the string <c>"42.5"</c> sits in the bag and no numeric range query
/// reaches it.
/// </para>
/// <para>
/// Only when it has to. A value already in its indexable form is left alone and no write is issued, so a
/// host that writes well-typed values pays nothing for this - which is what keeps the document provider's
/// near-zero write-time synchronization true rather than merely intended.
/// </para>
/// <para>
/// The eligibility rule is the shared one from <see cref="FlexFieldIndexManagerBase{TEntity}"/>: only
/// <c>Searchable</c> usages of an indexable field type. Flipping <c>Searchable</c> on therefore needs a
/// <see cref="RebuildAsync"/> here exactly as it does under EF Core, rather than the two providers answering
/// that question differently.
/// </para>
/// </summary>
/// <typeparam name="TMongoDbContext">The downstream's own MongoDB context interface.</typeparam>
/// <typeparam name="TEntity">The host entity type.</typeparam>
public abstract class MongoFlexFieldIndexManagerBase<TMongoDbContext, TEntity> : FlexFieldIndexManagerBase<TEntity>
    where TMongoDbContext : IAbpMongoDbContext
    where TEntity : class, IHasFlexFields, IEntity<Guid>
{
    protected IMongoDbContextProvider<TMongoDbContext> DbContextProvider { get; }

    protected IBasicRepository<TEntity> Repository { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// The BSON element the value bag lands in. Override only if the downstream mapped it elsewhere.
    /// </summary>
    protected virtual string FlexFieldsElementName => FlexFieldsMongoDbContextExtensions.DefaultFlexFieldsElementName;

    protected MongoFlexFieldIndexManagerBase(
        IMongoDbContextProvider<TMongoDbContext> dbContextProvider,
        IFlexFieldProvider<TEntity> flexFieldProvider,
        IBasicRepository<TEntity> repository,
        IFieldTypeResolver fieldTypeResolver,
        IUnitOfWorkManager unitOfWorkManager)
        : base(flexFieldProvider, fieldTypeResolver)
    {
        DbContextProvider = dbContextProvider;
        Repository = repository;
        UnitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Re-derives every host entity's bag and ensures the wildcard index exists. Ensuring the index here as
    /// well as at collection-configuration time is what makes "after this returns, queries are correct and
    /// index-served" mean the same thing under both providers; creating an index that already exists is a
    /// no-op.
    /// </summary>
    public override async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        await base.RebuildAsync(cancellationToken);
    }

    protected virtual async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync(cancellationToken);

        await dbContext.Collection<TEntity>().Indexes.CreateOneAsync(
            FlexFieldsMongoDbContextExtensions.CreateFlexFieldsIndexModel<TEntity>(FlexFieldsElementName),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Rewrites this entity's eligible bag values into their indexable form, and reports whether it had to.
    /// <para>
    /// Goes through <c>UpdateAsync</c> rather than relying on change tracking, for the same reason
    /// <see cref="FlexFieldValueMigrator{TEntity}"/> does: a document provider has none, so mutating the
    /// in-memory bag would otherwise never reach the collection.
    /// </para>
    /// </summary>
    protected override async Task<bool> SynchronizeCoreAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var changed = false;

        foreach (var indexable in await GetIndexableFieldsAsync(entity, cancellationToken))
        {
            var name = indexable.Value.Name;

            if (!entity.FlexFields.TryGetValue(name, out var rawValue) || rawValue == null)
            {
                continue;
            }

            var indexableValue = ToIndexableValue(indexable.IndexValueType, rawValue);
            if (AreEquivalent(rawValue, indexableValue))
            {
                continue;
            }

            entity.FlexFields[name] = indexableValue;
            changed = true;
        }

        if (changed)
        {
            await Repository.UpdateAsync(entity, autoSave: false, cancellationToken: cancellationToken);
        }

        return changed;
    }

    protected override Task FlushAsync(CancellationToken cancellationToken)
    {
        return UnitOfWorkManager.Current?.SaveChangesAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Reads one bag value into the form <paramref name="valueType"/> names, element-wise for a multi-valued
    /// field.
    /// <para>
    /// A multi-valued field type (Select, Tree) reports a single <see cref="IFieldType.IndexValueType"/> that
    /// describes each <i>member</i>, not the collection - the relational provider fans the collection out
    /// into one index row per member, so it never sees the collection as a value. This provider keeps the
    /// collection, because a BSON array is matched element-wise by an ordinary equality filter, which is the
    /// same fan-out expressed in storage rather than in rows. Reading the collection itself as a scalar would
    /// stringify it and destroy the values.
    /// </para>
    /// </summary>
    protected virtual object ToIndexableValue(FlexFieldValueType valueType, object rawValue)
    {
        if (!IsMultiValued(rawValue))
        {
            return FlexFieldValueConverter.Coerce(valueType, rawValue);
        }

        return ((IEnumerable)rawValue)
            .Cast<object?>()
            .Where(item => item != null)
            .Select(item => FlexFieldValueConverter.Coerce(valueType, item!))
            .ToList();
    }

    /// <summary>
    /// A string is a sequence of chars and a dictionary a sequence of pairs; neither is a multi-valued field
    /// value.
    /// </summary>
    protected static bool IsMultiValued(object value)
    {
        return value is IEnumerable and not string and not IDictionary;
    }

    protected static bool AreEquivalent(object current, object indexable)
    {
        if (IsMultiValued(current) && IsMultiValued(indexable))
        {
            return ((IEnumerable)current).Cast<object?>()
                .SequenceEqual(((IEnumerable)indexable).Cast<object?>());
        }

        return Equals(current, indexable);
    }
}
