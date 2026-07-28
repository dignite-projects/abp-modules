using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
            // One more than the cap, so exceeding it is detectable without reading the whole result - except
            // when the cap is already int.MaxValue (MaxMatchedIdCount + 1 would overflow to int.MinValue,
            // and MongoDB reads a negative Limit as "return one batch and stop", silently truncating instead
            // of throwing). There is no larger int to ask for at that point anyway, so fetching exactly
            // MaxMatchedIdCount is already the most this can request.
            .Limit(MaxMatchedIdCount < int.MaxValue ? MaxMatchedIdCount + 1 : MaxMatchedIdCount)
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
    /// <see cref="FlexFieldQueryCondition.FieldId"/> and never reads <see cref="FlexFieldQueryCondition.FieldName"/>
    /// at all, this provider has only the bag to address - and a bag holds no field ids - so this is the one
    /// thing that actually makes a condition usable here.
    /// </summary>
    protected virtual string GetFieldPath(FlexFieldQueryCondition condition)
    {
        var name = condition.FieldName;

        // FieldName is a required constructor parameter, so this guards against a blank one slipping
        // through - not a missing one, which the type no longer allows at all.
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AbpException(
                $"{nameof(FlexFieldQueryCondition)}.{nameof(FlexFieldQueryCondition.FieldName)} is blank on " +
                $"the condition for field ({condition.FieldId}), and the MongoDB provider needs it: a value " +
                "bag is keyed by field name, so there is nothing else to build a path from. Supply the " +
                "field's current, non-empty name.");
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

    /// <summary>
    /// Whether <see cref="FlexFieldQueryOperator.Contains"/> ignores case. Left <c>false</c> by default -
    /// changing it changes what a caller's existing conditions match, so that is this executor's decision to
    /// make, not the kernel's to impose.
    /// <para>
    /// There is no single relational answer to match here either way: the EF Core provider's
    /// <c>StringValue.Contains(...)</c> takes whatever case sensitivity the downstream's own database
    /// collation gives it - case-sensitive on SQLite (translated to <c>instr()</c>), case-insensitive on SQL
    /// Server's usual default collation, and so on. A downstream that knows its own EF deployment's collation
    /// overrides this to match it.
    /// </para>
    /// </summary>
    protected virtual bool ContainsIsCaseInsensitive => false;

    protected virtual FilterDefinition<TEntity> BuildStringFilter(string path, FlexFieldQueryCondition condition)
    {
        var builder = Builders<TEntity>.Filter;

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => builder.Eq(path, condition.Value),
            FlexFieldQueryOperator.NotEquals => NotEquals(path, condition.Value),
            // Unanchored, so this is a full scan of the wildcard index - not a bounded seek - exactly as the
            // relational provider's non-sargable LIKE '%x%' never seeks a B-tree index either; both read
            // everything and test each value, just over a smaller structure than the raw documents.
            // Escaped because the value is data, not a pattern the caller is offering.
            FlexFieldQueryOperator.Contains => builder.And(
                builder.Exists(path),
                builder.Regex(path, new BsonRegularExpression(
                    Regex.Escape(condition.Value), ContainsIsCaseInsensitive ? "i" : ""))),
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
    /// Matches what the EF Core provider's <c>x.StringValue != condition.Value</c> (etc.) actually returns
    /// for a host whose field is multi-valued, not MongoDB's own reading of <c>$ne</c> against an array.
    /// <para>
    /// The relational provider fans a multi-valued field out into one pivot row per selected value, so its
    /// <c>!=</c> is existential per row: a host is included the moment <i>any</i> of its rows differs from
    /// <paramref name="value"/> - the same row-at-a-time mechanism that makes <c>Equals</c> existential on
    /// both providers already ("does any row/element match"). MongoDB's own <c>$ne</c> on an array field is
    /// the opposite: it matches only when <i>no</i> element equals the value - the universal reading, not the
    /// existential one. Left as plain <c>$ne</c>, a multi-valued field would return the logically opposite
    /// answer from the relational provider for the same condition and the same data.
    /// </para>
    /// <para>
    /// So a multi-valued field is matched with <c>$elemMatch: {$ne: value}</c> - "at least one element
    /// differs" - instead, in an <c>$or</c> alongside the scalar reading, since <c>$elemMatch</c> never
    /// matches a non-array value at all. The scalar branch excludes arrays explicitly (<c>$not $type
    /// array</c>) so an array is judged only by <c>$elemMatch</c>, never additionally by a plain <c>$ne</c>
    /// that would otherwise also (vacuously) match an empty array with no elements to differ from anything.
    /// </para>
    /// <para>
    /// <c>$ne</c> on its own, either way, also matches documents that have no such key at all, which the
    /// relational provider never does - its index has no row to match. Pairing the scalar branch with
    /// <c>$exists</c>, and relying on <c>$elemMatch</c>'s own requirement that a matching element actually
    /// exist, keeps that true here too.
    /// </para>
    /// </summary>
    protected virtual FilterDefinition<TEntity> NotEquals<TValue>(string path, TValue value)
    {
        var builder = Builders<TEntity>.Filter;
        var neFilter = builder.Ne(path, value);

        var scalarBranch = builder.And(builder.Exists(path), builder.Not(builder.Type(path, BsonType.Array)), neFilter);
        var arrayBranch = new BsonDocument(path, new BsonDocument("$elemMatch", RenderOperator(path, neFilter)));

        return builder.Or(scalarBranch, arrayBranch);
    }

    /// <summary>
    /// Renders <paramref name="filter"/> - built against <paramref name="path"/> for the whole document -
    /// down to just its own operator sub-document (<c>{$ne: &lt;value&gt;}</c>), so it can be nested inside
    /// <c>$elemMatch</c> instead. Rendering rather than hand-building the equivalent BSON is what reuses the
    /// serializer registered for the bag's value type (<see cref="FlexFieldBagValueSerializer"/>) - the
    /// filter builder's typed <c>Ne&lt;TValue&gt;</c> overload resolves it the same way a top-level
    /// comparison against this path already does, so a hand-written value here could not silently drift from
    /// it.
    /// </summary>
    private static BsonDocument RenderOperator(string path, FilterDefinition<TEntity> filter)
    {
        var rendered = filter.Render(new RenderArgs<TEntity>(
            BsonSerializer.SerializerRegistry.GetSerializer<TEntity>(), BsonSerializer.SerializerRegistry));

        return rendered[path].AsBsonDocument;
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
