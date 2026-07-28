using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="IFlexFieldQueryExecutor{TEntity}"/>. Every comparison runs as a
/// native filter against a path into the value bag - <c>FlexFields.&lt;name&gt;</c> - which is what the
/// wildcard index from <see cref="FlexFieldsMongoDbContextExtensions.ConfigureFlexFieldsIndex"/> serves.
/// There is no pivot collection to join to; the conditions all apply to the same document, so they compose
/// into a single <c>$and</c>.
/// <para>
/// Two phases, and deliberately so. The contract hands back an <see cref="IQueryable{T}"/> for the host to
/// keep composing paging and sorting onto, and the driver's LINQ provider has no supported way to inject a
/// raw filter into one (<c>Inject</c> was LINQ2-only and is gone in driver 3.x). So the filter runs first as
/// a real query projecting only <c>_id</c>, and the result narrows the host's query with an <c>$in</c>. The
/// last line is word for word the EF Core provider's - the difference is only that EF folds its ids into a
/// subquery while this materializes them, costing one extra round trip in total rather than one per
/// condition.
/// </para>
/// <para>
/// The id list is bounded by <see cref="MaxMatchedIdCount"/> and exceeding it throws, because silently
/// building an unbounded <c>$in</c> is how this degrades into a memory problem in production rather than in
/// a test.
/// </para>
/// </summary>
/// <typeparam name="TMongoDbContext">The downstream's own MongoDB context interface.</typeparam>
/// <typeparam name="TEntity">The host entity type being filtered.</typeparam>
public abstract class MongoFlexFieldQueryExecutorBase<TMongoDbContext, TEntity> : IFlexFieldQueryExecutor<TEntity>
    where TMongoDbContext : IAbpMongoDbContext
    where TEntity : class, IHasFlexFields, IEntity<Guid>
{
    protected IMongoDbContextProvider<TMongoDbContext> DbContextProvider { get; }

    /// <summary>
    /// The BSON element the value bag lands in. Override only if the downstream mapped it elsewhere.
    /// </summary>
    protected virtual string FlexFieldsElementName => FlexFieldsMongoDbContextExtensions.DefaultFlexFieldsElementName;

    /// <summary>
    /// How many matching ids may be carried into the host's query. Raise it for a host that legitimately
    /// matches more; a filter that matches most of a large collection is better expressed as the host's own
    /// query with a flex field condition narrowing it, not the other way round.
    /// </summary>
    protected virtual int MaxMatchedIdCount => 10_000;

    protected MongoFlexFieldQueryExecutorBase(IMongoDbContextProvider<TMongoDbContext> dbContextProvider)
    {
        DbContextProvider = dbContextProvider;
    }

    public virtual async Task<IQueryable<TEntity>> ApplyFilterAsync(
        IQueryable<TEntity> query,
        IReadOnlyList<FlexFieldQueryCondition> conditions,
        CancellationToken cancellationToken = default)
    {
        FlexFieldQueryConditions.EnsureNotEmpty(conditions);

        var matchingIds = await FindMatchingIdsAsync(conditions, cancellationToken);

        return query.Where(e => matchingIds.Contains(e.Id));
    }

    protected virtual async Task<List<Guid>> FindMatchingIdsAsync(
        IReadOnlyList<FlexFieldQueryCondition> conditions,
        CancellationToken cancellationToken)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync(cancellationToken);
        var filter = Builders<TEntity>.Filter.And(conditions.Select(BuildFilter));

        var ids = await dbContext.Collection<TEntity>()
            .Find(filter)
            .Project(e => e.Id)
            // One more than the cap, so exceeding it is detectable without reading the whole result.
            .Limit(MaxMatchedIdCount + 1)
            .ToListAsync(cancellationToken);

        if (ids.Count > MaxMatchedIdCount)
        {
            throw new AbpException(
                $"A flex field filter matched more than {MaxMatchedIdCount} entities of type " +
                $"({typeof(TEntity).Name}). Narrow the conditions, or raise {nameof(MaxMatchedIdCount)} on " +
                "this executor if the host really does match that many.");
        }

        return ids;
    }

    protected virtual FilterDefinition<TEntity> BuildFilter(FlexFieldQueryCondition condition)
    {
        FlexFieldQueryConditions.EnsureOperatorSupported(condition);

        var path = GetFieldPath(condition);

        return condition.ValueType switch
        {
            FlexFieldValueType.String => BuildStringFilter(path, condition),
            FlexFieldValueType.Number => BuildComparableFilter<decimal>(path, condition),
            FlexFieldValueType.DateTime => BuildComparableFilter<DateTime>(path, condition),
            FlexFieldValueType.Boolean => BuildEqualityFilter<bool>(path, condition),
            FlexFieldValueType.Guid => BuildEqualityFilter<Guid>(path, condition),
            _ => throw new AbpException($"Unknown {nameof(FlexFieldValueType)} ({condition.ValueType}).")
        };
    }

    /// <summary>
    /// The path a condition addresses. Unlike the relational provider, which finds its rows by
    /// <see cref="FlexFieldQueryCondition.FieldId"/>, this provider has only the bag to address - and a bag
    /// holds no field ids - so <see cref="FlexFieldQueryCondition.FieldName"/> is required rather than
    /// optional here.
    /// </summary>
    protected virtual string GetFieldPath(FlexFieldQueryCondition condition)
    {
        var name = condition.FieldName;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AbpException(
                $"{nameof(FlexFieldQueryCondition)}.{nameof(FlexFieldQueryCondition.FieldName)} is required " +
                $"by the MongoDB provider but was not set on the condition for field ({condition.FieldId}). " +
                "A value bag is keyed by field name, so there is nothing else to build a path from - supply " +
                "the field's current name alongside its id.");
        }

        // A field name goes straight into a BSON path, so one that contains a path separator or starts a
        // BSON operator would address something other than the field it names.
        if (name.Contains('.') || name.StartsWith('$'))
        {
            throw new AbpException(
                $"The flex field name ('{name}') cannot be addressed as a BSON path: a name used with the " +
                "MongoDB provider must not contain '.' or start with '$'.");
        }

        return $"{FlexFieldsElementName}.{name}";
    }

    protected virtual FilterDefinition<TEntity> BuildStringFilter(string path, FlexFieldQueryCondition condition)
    {
        var builder = Builders<TEntity>.Filter;

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => builder.Eq(path, condition.Value),
            FlexFieldQueryOperator.NotEquals => NotEquals(path, condition.Value),
            // Unanchored, so it is a collection scan exactly as the relational provider's LIKE '%x%' is.
            // Escaped because the value is data, not a pattern the caller is offering.
            FlexFieldQueryOperator.Contains => builder.And(
                builder.Exists(path),
                builder.Regex(path, new BsonRegularExpression(Regex.Escape(condition.Value)))),
            FlexFieldQueryOperator.In => builder.In(path, FlexFieldValueConverter.SplitValues(condition.Value)),
            _ => throw FlexFieldQueryConditions.UnsupportedOperator(condition)
        };
    }

    protected virtual FilterDefinition<TEntity> BuildComparableFilter<TValue>(string path, FlexFieldQueryCondition condition)
    {
        var builder = Builders<TEntity>.Filter;

        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            return builder.In(path, ParseList<TValue>(condition));
        }

        var value = Parse<TValue>(condition);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => builder.Eq(path, value),
            FlexFieldQueryOperator.NotEquals => NotEquals(path, value),
            FlexFieldQueryOperator.GreaterThan => builder.Gt(path, value),
            FlexFieldQueryOperator.GreaterThanOrEqual => builder.Gte(path, value),
            FlexFieldQueryOperator.LessThan => builder.Lt(path, value),
            FlexFieldQueryOperator.LessThanOrEqual => builder.Lte(path, value),
            _ => throw FlexFieldQueryConditions.UnsupportedOperator(condition)
        };
    }

    protected virtual FilterDefinition<TEntity> BuildEqualityFilter<TValue>(string path, FlexFieldQueryCondition condition)
    {
        var builder = Builders<TEntity>.Filter;

        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            return builder.In(path, ParseList<TValue>(condition));
        }

        var value = Parse<TValue>(condition);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => builder.Eq(path, value),
            FlexFieldQueryOperator.NotEquals => NotEquals(path, value),
            _ => throw FlexFieldQueryConditions.UnsupportedOperator(condition)
        };
    }

    /// <summary>
    /// <c>$ne</c> on its own also matches documents that have no such key at all, which the relational
    /// provider never does - its index has no row to match. Pairing it with <c>$exists</c> makes "not equal
    /// to" mean the same thing under both providers: the host has a value for this field, and it is not
    /// that one.
    /// </summary>
    protected virtual FilterDefinition<TEntity> NotEquals<TValue>(string path, TValue value)
    {
        var builder = Builders<TEntity>.Filter;
        return builder.And(builder.Exists(path), builder.Ne(path, value));
    }

    protected static TValue Parse<TValue>(FlexFieldQueryCondition condition)
    {
        return (TValue)FlexFieldValueConverter.Parse(condition.ValueType, condition.Value);
    }

    protected static List<TValue> ParseList<TValue>(FlexFieldQueryCondition condition)
    {
        return FlexFieldValueConverter.ParseList(condition.ValueType, condition.Value)
            .Cast<TValue>()
            .ToList();
    }
}
