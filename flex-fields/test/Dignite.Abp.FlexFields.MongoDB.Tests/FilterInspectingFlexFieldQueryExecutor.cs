using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Exposes the filter the executor builds, so a test can assert on the query that is actually sent rather
/// than only on the answers it comes back with. The MongoDB counterpart of the EF Core test project's
/// SQL-inspecting executor, and it exists for the same reason: a filter that quietly stopped being
/// index-servable would still return the right rows.
/// <para>
/// Exposed as itself only, so it does not race the real
/// <see cref="TestArticleFlexFieldQueryExecutor"/> registration.
/// </para>
/// </summary>
[ExposeServices(typeof(FilterInspectingFlexFieldQueryExecutor))]
public class FilterInspectingFlexFieldQueryExecutor
    : MongoFlexFieldQueryExecutorBase<ITestFlexFieldsMongoDbContext, TestArticle>,
      ITransientDependency
{
    public FilterInspectingFlexFieldQueryExecutor(
        IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public BsonDocument Render(FlexFieldQueryCondition condition)
    {
        return RenderCore(BuildFilter(condition));
    }

    /// <summary>Composes conditions exactly as <c>FindMatchingIdsAsync</c> does.</summary>
    public BsonDocument RenderAll(params FlexFieldQueryCondition[] conditions)
    {
        return RenderCore(Builders<TestArticle>.Filter.And(conditions.Select(BuildFilter)));
    }

    private static BsonDocument RenderCore(FilterDefinition<TestArticle> filter)
    {
        return filter.Render(new RenderArgs<TestArticle>(
            BsonSerializer.SerializerRegistry.GetSerializer<TestArticle>(),
            BsonSerializer.SerializerRegistry));
    }
}
