using System;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Text;
using MongoDB.Bson;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// The design's central claim, in this provider's terms: querying by field is a native filter on a path into
/// the value bag, served by the wildcard index - not a filter applied to already-materialized host entities,
/// and not a collection scan. Asserted against the filter that is actually sent and against MongoDB's own
/// query plan, because a query that silently degraded would still return the right answers in these tests -
/// only slowly, and only in production.
/// </summary>
public class FlexFieldQueryPushdown_Tests : FlexFieldsMongoDbTestBase
{
    private const string CollectionName = "TestArticles";

    private FilterInspectingFlexFieldQueryExecutor Executor =>
        GetRequiredService<FilterInspectingFlexFieldQueryExecutor>();

    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    [Fact]
    public void A_string_condition_becomes_a_filter_on_the_bag_path()
    {
        var filter = Executor.Render(new FlexFieldQueryCondition(
            Guid.NewGuid(), "Title", FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String));

        filter.ShouldBe(BsonDocument.Parse("{ 'FlexFields.Title': 'Alpha' }"));
    }

    [Fact]
    public void A_number_condition_compares_against_a_bson_number_not_a_string()
    {
        var filter = Executor.Render(new FlexFieldQueryCondition(
            Guid.NewGuid(), "Price", FlexFieldQueryOperator.GreaterThanOrEqual, "15", FlexFieldValueType.Number));

        filter["FlexFields.Price"]["$gte"].BsonType.ShouldBe(BsonType.Decimal128);
    }

    [Fact]
    public void An_In_condition_becomes_a_set_membership_test()
    {
        var filter = Executor.Render(new FlexFieldQueryCondition(
            Guid.NewGuid(), "Tags", FlexFieldQueryOperator.In, "red,blue", FlexFieldValueType.String));

        filter.ShouldBe(BsonDocument.Parse("{ 'FlexFields.Tags': { '$in': ['red', 'blue'] } }"));
    }

    [Fact]
    public void NotEquals_also_requires_the_field_to_be_present()
    {
        var filter = Executor.Render(new FlexFieldQueryCondition(
            Guid.NewGuid(), "Title", FlexFieldQueryOperator.NotEquals, "Alpha", FlexFieldValueType.String));

        // The scalar branch: $ne alone would also match a host that has no Title at all, and (if Title were
        // ever an array) an empty one - $exists plus excluding arrays keeps this branch scoped to a genuine
        // scalar value that differs. See FlexFieldQueryExecutor_Tests for the behavioural (not just
        // rendering) proof, on both a scalar and a multi-valued field.
        filter.ShouldBe(BsonDocument.Parse("""
            {
                '$or': [
                    { 'FlexFields.Title': { '$exists': true, '$not': { '$type': 4 }, '$ne': 'Alpha' } },
                    { 'FlexFields.Title': { '$elemMatch': { '$ne': 'Alpha' } } }
                ]
            }
            """));
    }

    [Fact]
    public void Every_condition_folds_into_one_filter()
    {
        // One document holds every field, so unlike the relational provider - whose conditions each match a
        // different pivot row and so each need their own subquery - these compose into a single query.
        var filter = Executor.RenderAll(
            new FlexFieldQueryCondition(Guid.NewGuid(), "Title", FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String),
            new FlexFieldQueryCondition(Guid.NewGuid(), "Price", FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number));

        filter.Names.ShouldBe(new[] { "FlexFields.Title", "FlexFields.Price" }, ignoreOrder: true);
    }

    [Fact]
    public async Task An_equality_condition_is_served_by_the_wildcard_index()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));

        var plan = await ExplainAsync(Executor.Render(
            Condition(title, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String)));

        plan.ShouldContain("IXSCAN");
        plan.ShouldNotContain("COLLSCAN");
    }

    [Fact]
    public async Task A_number_range_condition_is_served_by_the_wildcard_index()
    {
        var price = Provider.AddDefinition("Price", NumberFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        var plan = await ExplainAsync(Executor.Render(
            Condition(price, FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number)));

        plan.ShouldContain("IXSCAN");
        plan.ShouldNotContain("COLLSCAN");
    }

    /// <summary>
    /// The winning plan as MongoDB reports it, flattened to text so a test can say which stages are in it
    /// without walking a plan tree whose shape varies by server version.
    /// </summary>
    private async Task<string> ExplainAsync(BsonDocument filter)
    {
        var plan = string.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();

            var explain = await dbContext.Database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                {
                    "explain", new BsonDocument
                    {
                        { "find", CollectionName },
                        { "filter", filter }
                    }
                },
                { "verbosity", "queryPlanner" }
            });

            plan = explain["queryPlanner"]["winningPlan"].ToJson();
        });

        return plan;
    }
}
