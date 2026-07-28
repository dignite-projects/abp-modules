using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Switch;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MongoDB;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Querying by field goes through native path filters against the value bag, never a filter over
/// already-materialized host entities. Mirrors the EF Core provider's executor tests scenario for scenario,
/// which is the point: the two providers answer the same questions the same way even though one reads a
/// pivot table and the other reads the bag.
/// </summary>
public class FlexFieldQueryExecutor_Tests : FlexFieldsMongoDbTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldQueryExecutor<TestArticle> Executor => GetRequiredService<IFlexFieldQueryExecutor<TestArticle>>();

    [Fact]
    public async Task Single_equality_condition_matches()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var alpha = await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));
        await IndexedArticleAsync(a => a.SetField("Title", "Beta"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String));

        ids.ShouldBe(new[] { alpha });
    }

    [Fact]
    public async Task Contains_condition_matches_substrings()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Title", "Hello world"));
        await IndexedArticleAsync(a => a.SetField("Title", "Goodbye"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.Contains, "lo wo", FlexFieldValueType.String));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task Contains_is_case_sensitive_by_default()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("Title", "Hello world"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.Contains, "hello", FlexFieldValueType.String));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContainsIsCaseInsensitive_can_be_overridden_to_match_a_case_insensitive_ef_deployment()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Title", "Hello world"));

        IReadOnlyList<Guid> ids = Array.Empty<Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            var executor = new CaseInsensitiveContainsQueryExecutor(
                GetRequiredService<IMongoDbContextProvider<ITestFlexFieldsMongoDbContext>>());
            var queryable = await GetRequiredService<IRepository<TestArticle, Guid>>().GetQueryableAsync();
            var filtered = await executor.ApplyFilterAsync(queryable, new[]
            {
                Condition(title, FlexFieldQueryOperator.Contains, "hello", FlexFieldValueType.String)
            });
            ids = filtered.Select(a => a.Id).ToList();
        });

        ids.ShouldBe(new[] { matching });
    }

    private class CaseInsensitiveContainsQueryExecutor : MongoFlexFieldQueryExecutorBase<ITestFlexFieldsMongoDbContext, TestArticle>
    {
        protected override bool ContainsIsCaseInsensitive => true;

        public CaseInsensitiveContainsQueryExecutor(IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }

    [Fact]
    public async Task Contains_treats_its_value_as_text_not_as_a_pattern()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Title", "a.c"));
        await IndexedArticleAsync(a => a.SetField("Title", "abc"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.Contains, "a.c", FlexFieldValueType.String));

        // Unescaped, '.' would match any character and pull in "abc" too.
        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task Multiple_conditions_intersect_across_different_value_types()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var viewCount = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var matching = await IndexedArticleAsync(a =>
        {
            a.SetField("Title", "Alpha");
            a.SetField("ViewCount", 20);
        });
        await IndexedArticleAsync(a =>
        {
            a.SetField("Title", "Alpha");
            a.SetField("ViewCount", 5);
        });

        var ids = await QueryAsync(
            Condition(title, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String),
            Condition(viewCount, FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task Number_ranges_compare_numerically_not_lexically()
    {
        var viewCount = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);
        var bigger = await IndexedArticleAsync(a => a.SetField("ViewCount", 100));
        await IndexedArticleAsync(a => a.SetField("ViewCount", 9));

        // Lexically "100" sorts before "9"; numerically it does not.
        var ids = await QueryAsync(Condition(viewCount, FlexFieldQueryOperator.GreaterThan, "10", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { bigger });
    }

    [Fact]
    public async Task Number_ranges_reach_a_value_that_arrived_as_text()
    {
        // The whole reason the index manager rewrites the bag: an unsynchronized "42.5" is a BSON string and
        // no numeric range filter reaches it.
        var price = Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        var ids = await QueryAsync(Condition(price, FlexFieldQueryOperator.GreaterThanOrEqual, "42", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task DateTime_ranges_match()
    {
        var publishedAt = Provider.AddDefinition("PublishedAt", DateTimeFieldType.ControlName);
        var later = await IndexedArticleAsync(a => a.SetField("PublishedAt", "2025-06-01T00:00:00"));
        await IndexedArticleAsync(a => a.SetField("PublishedAt", "2024-01-01T00:00:00"));

        var ids = await QueryAsync(Condition(
            publishedAt, FlexFieldQueryOperator.GreaterThan, "2025-01-01T00:00:00", FlexFieldValueType.DateTime));

        ids.ShouldBe(new[] { later });
    }

    [Fact]
    public async Task Boolean_equality_matches()
    {
        var isFeatured = Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName);
        var featured = await IndexedArticleAsync(a => a.SetField("IsFeatured", true));
        await IndexedArticleAsync(a => a.SetField("IsFeatured", false));

        var ids = await QueryAsync(Condition(isFeatured, FlexFieldQueryOperator.Equals, "true", FlexFieldValueType.Boolean));

        ids.ShouldBe(new[] { featured });
    }

    [Fact]
    public async Task A_multi_valued_field_matches_on_any_of_its_members()
    {
        // The document-store form of the relational provider's fan-out: the members are a BSON array, so an
        // ordinary equality filter matches element-wise with no separate rows to write.
        var tags = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "red", "blue" }));
        await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "green" }));

        var ids = await QueryAsync(Condition(tags, FlexFieldQueryOperator.Equals, "blue", FlexFieldValueType.String));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task In_condition_matches_any_listed_value()
    {
        var tags = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        var red = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "red" }));
        var green = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "green" }));
        await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "black" }));

        var ids = await QueryAsync(Condition(tags, FlexFieldQueryOperator.In, "red, green", FlexFieldValueType.String));

        ids.ShouldBe(new[] { red, green }, ignoreOrder: true);
    }

    [Fact]
    public async Task Number_conditions_parse_with_invariant_culture()
    {
        var price = Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Price", 42.5m));

        var ids = await QueryAsync(Condition(price, FlexFieldQueryOperator.Equals, "42.5", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task A_condition_that_matches_nothing_returns_empty()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.Equals, "Nope", FlexFieldValueType.String));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_impossible_intersection_short_circuits_to_empty()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var viewCount = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);
        await IndexedArticleAsync(a =>
        {
            a.SetField("Title", "Alpha");
            a.SetField("ViewCount", 20);
        });

        var ids = await QueryAsync(
            Condition(title, FlexFieldQueryOperator.Equals, "Nope", FlexFieldValueType.String),
            Condition(viewCount, FlexFieldQueryOperator.Equals, "20", FlexFieldValueType.Number));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotEquals_does_not_match_a_host_that_has_no_such_field()
    {
        // MongoDB's $ne on its own matches a missing key too, which the relational provider never does - it
        // has no index row to match. Paired with $exists so "not equal to" means the same thing under both.
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var other = await IndexedArticleAsync(a => a.SetField("Title", "Beta"));
        await IndexedArticleAsync(a => a.SetField("Unrelated", "no Title at all"));

        var ids = await QueryAsync(Condition(title, FlexFieldQueryOperator.NotEquals, "Alpha", FlexFieldValueType.String));

        ids.ShouldBe(new[] { other });
    }

    [Fact]
    public async Task NotEquals_matches_a_multi_valued_field_with_at_least_one_differing_element()
    {
        // The relational provider fans a multi-valued field out into one pivot row per element, so its
        // NotEquals is existential ("some row differs") - the same per-row mechanism that already makes
        // Equals existential on both providers. MongoDB's own $ne on an array is the opposite (universal:
        // "no element equals"), so this field type needs its own reading rather than plain $ne to agree
        // with EF Core on the same data.
        var tags = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        var mixed = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "red", "blue" }));
        var onlyBlue = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "blue" }));
        await IndexedArticleAsync(a => a.SetField("Tags", new List<string>()));

        var ids = await QueryAsync(Condition(tags, FlexFieldQueryOperator.NotEquals, "blue", FlexFieldValueType.String));

        // mixed has "red", which differs from "blue" - included, matching EF's per-row semantics.
        // onlyBlue's single value equals "blue" - excluded, same as a scalar field would be.
        // An empty selection has no value to differ from anything - excluded, same as EF's 0 pivot rows.
        ids.ShouldBe(new[] { mixed });
    }

    [Fact]
    public async Task An_empty_condition_list_is_rejected()
    {
        await Should.ThrowAsync<ArgumentException>(() => QueryAsync());
    }

    [Fact]
    public async Task Raising_MaxMatchedIdCount_to_int_MaxValue_does_not_overflow_the_fetch_limit()
    {
        // MaxMatchedIdCount + 1 overflows to int.MinValue at this boundary, and MongoDB.Driver's own Limit()
        // throws trying to negate that (OverflowException) rather than degrading gracefully - exactly the
        // "raise it if the host really does match that many" escape hatch the property's own doc invites.
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));

        IReadOnlyList<Guid> ids = Array.Empty<Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            var executor = new UnboundedMaxCountQueryExecutor(
                GetRequiredService<IMongoDbContextProvider<ITestFlexFieldsMongoDbContext>>());
            var queryable = await GetRequiredService<IRepository<TestArticle, Guid>>().GetQueryableAsync();
            var filtered = await executor.ApplyFilterAsync(queryable, new[]
            {
                Condition(title, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String)
            });
            ids = filtered.Select(a => a.Id).ToList();
        });

        ids.ShouldBe(new[] { matching });
    }

    private class UnboundedMaxCountQueryExecutor : MongoFlexFieldQueryExecutorBase<ITestFlexFieldsMongoDbContext, TestArticle>
    {
        protected override int MaxMatchedIdCount => int.MaxValue;

        public UnboundedMaxCountQueryExecutor(IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
    }

    [Fact]
    public async Task An_operator_the_value_type_does_not_support_is_rejected()
    {
        var isFeatured = Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName);

        await Should.ThrowAsync<AbpException>(() => QueryAsync(
            Condition(isFeatured, FlexFieldQueryOperator.In, "true,false", FlexFieldValueType.Boolean)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_condition_with_a_blank_field_name_is_rejected(string blankName)
    {
        // FieldName is a required constructor parameter, so a condition can no longer be built without one
        // at all - this is the one way a caller can still hand this provider a name it cannot address, since
        // a bag holds no field ids and this provider needs the name to build a path.
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);

        var exception = await Should.ThrowAsync<AbpException>(() => QueryAsync(
            new FlexFieldQueryCondition(title.Field.Id, blankName, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String)));

        exception.Message.ShouldContain(nameof(FlexFieldQueryCondition.FieldName));
        exception.Message.ShouldContain(title.Field.Id.ToString());
    }

    [Theory]
    [InlineData("has.a.dot")]
    [InlineData("$starts")]
    public async Task A_field_name_that_is_not_addressable_as_a_bson_path_is_rejected(string fieldName)
    {
        var exception = await Should.ThrowAsync<AbpException>(() => QueryAsync(new FlexFieldQueryCondition(
            Guid.NewGuid(), fieldName, FlexFieldQueryOperator.Equals, "x", FlexFieldValueType.String)));

        exception.Message.ShouldContain(fieldName);
    }

    private async Task<IReadOnlyList<Guid>> QueryAsync(params FlexFieldQueryCondition[] conditions)
    {
        IReadOnlyList<Guid> ids = Array.Empty<Guid>();

        await WithUnitOfWorkAsync(async () =>
        {
            var queryable = await GetRequiredService<IRepository<TestArticle, Guid>>().GetQueryableAsync();
            var filtered = await Executor.ApplyFilterAsync(queryable, conditions);
            ids = filtered.Select(a => a.Id).ToList();
        });

        return ids;
    }
}
