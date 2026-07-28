using MongoDB.Bson;
using MongoDB.Driver;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Support, not ownership: the downstream opens the collection configuration for its <i>own</i> host entity
/// and calls this to get the one index the kernel's contracts need. Modelled on the EF Core provider's
/// <c>FlexFieldsDbContextModelCreatingExtensions</c>, which likewise only ever extends a builder the
/// downstream already opened.
/// <para>
/// Consequently this package names no collection, no connection string and no prefix, and cannot force an
/// entity into anyone's model.
/// </para>
/// </summary>
public static class FlexFieldsMongoDbContextExtensions
{
    /// <summary>
    /// The BSON element the value bag lands in. Pinned to the property name so the path is identical across
    /// all downstreams, the same way the EF Core provider pins the column name with
    /// <c>HasColumnName(nameof(IHasFlexFields.FlexFields))</c>.
    /// </summary>
    public const string DefaultFlexFieldsElementName = nameof(IHasFlexFields.FlexFields);

    /// <summary>
    /// Creates the wildcard index over a host's value bag. Call from the downstream's own
    /// <c>ConfigureIndexes</c> for its host entity:
    /// <code>
    /// builder.Entity&lt;Entry&gt;(x =&gt;
    /// {
    ///     x.CollectionName = "Entries";
    ///     x.ConfigureIndexes(indexes =&gt; indexes.ConfigureFlexFieldsIndex());
    /// });
    /// </code>
    /// <para>
    /// One wildcard index rather than one index per searchable field, because the set of keys in the bag is
    /// data, not schema: fields are added, renamed and made searchable at runtime, and a per-field index
    /// would have to be created and dropped in step with all of that - and would run into MongoDB's limit of
    /// 64 indexes per collection long before a real field catalogue ran out. A wildcard index covers every
    /// key present and future, including ones a rename introduces, with no maintenance at all.
    /// </para>
    /// <para>
    /// The trade is that a wildcard index serves one path per query and cannot be compounded with a host's
    /// own columns. A downstream that needs a specific compound index can add it alongside this one.
    /// </para>
    /// </summary>
    public static void ConfigureFlexFieldsIndex(
        this IMongoIndexManager<BsonDocument> indexes,
        string? elementName = null)
    {
        indexes.CreateOne(CreateFlexFieldsIndexModel<BsonDocument>(elementName));
    }

    /// <summary>
    /// The same index as a model, for callers holding a typed collection rather than a model builder -
    /// notably <c>MongoFlexFieldIndexManagerBase.RebuildAsync</c>, which ensures the index exists as part of
    /// making queries correct and fast again.
    /// </summary>
    public static CreateIndexModel<TDocument> CreateFlexFieldsIndexModel<TDocument>(string? elementName = null)
    {
        // Written as an explicit key document rather than through the typed wildcard builder so the produced
        // path is exactly this and does not depend on how the builder composes a sub-path.
        return new CreateIndexModel<TDocument>(
            new BsonDocument($"{elementName ?? DefaultFlexFieldsElementName}.$**", 1));
    }
}
