using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Switch;
using Dignite.Abp.FlexFields.Text;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Querying by field always goes through the index, pushed down to SQL - never a filter over
/// already-materialized host entities.
/// </summary>
public class FlexFieldQueryExecutor_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldIndexManager<TestArticle> Manager => GetRequiredService<IFlexFieldIndexManager<TestArticle>>();

    private IFlexFieldQueryExecutor<TestArticle> Executor => GetRequiredService<IFlexFieldQueryExecutor<TestArticle>>();

    [Fact]
    public async Task Single_equality_condition_matches()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var alpha = await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));
        await IndexedArticleAsync(a => a.SetField("Title", "Beta"));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            title.Field.Id, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String));

        ids.ShouldBe(new[] { alpha });
    }

    [Fact]
    public async Task Contains_condition_matches_substrings()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Title", "Hello world"));
        await IndexedArticleAsync(a => a.SetField("Title", "Goodbye"));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            title.Field.Id, FlexFieldQueryOperator.Contains, "lo wo", FlexFieldValueType.String));

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
        await IndexedArticleAsync(a =>
        {
            a.SetField("Title", "Beta");
            a.SetField("ViewCount", 20);
        });

        var ids = await QueryAsync(
            new FlexFieldQueryCondition(title.Field.Id, FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String),
            new FlexFieldQueryCondition(viewCount.Field.Id, FlexFieldQueryOperator.GreaterThanOrEqual, "15", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task Number_range_operators_filter_numerically_not_lexically()
    {
        var viewCount = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);
        // 9 vs 100: a lexical comparison would order these the other way round.
        var small = await IndexedArticleAsync(a => a.SetField("ViewCount", 9));
        var large = await IndexedArticleAsync(a => a.SetField("ViewCount", 100));

        (await QueryAsync(new FlexFieldQueryCondition(
                viewCount.Field.Id, FlexFieldQueryOperator.GreaterThan, "50", FlexFieldValueType.Number)))
            .ShouldBe(new[] { large });

        (await QueryAsync(new FlexFieldQueryCondition(
                viewCount.Field.Id, FlexFieldQueryOperator.LessThan, "50", FlexFieldValueType.Number)))
            .ShouldBe(new[] { small });
    }

    [Fact]
    public async Task DateTime_range_operators_filter()
    {
        var publishedAt = Provider.AddDefinition("PublishedAt", DateTimeFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("PublishedAt", new DateTime(2020, 1, 1)));
        var late = await IndexedArticleAsync(a => a.SetField("PublishedAt", new DateTime(2025, 1, 1)));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            publishedAt.Field.Id, FlexFieldQueryOperator.GreaterThan, "2024-01-01", FlexFieldValueType.DateTime));

        ids.ShouldBe(new[] { late });
    }

    [Fact]
    public async Task Boolean_equality_condition_matches()
    {
        var isFeatured = Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName);
        var featured = await IndexedArticleAsync(a => a.SetField("IsFeatured", true));
        await IndexedArticleAsync(a => a.SetField("IsFeatured", false));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            isFeatured.Field.Id, FlexFieldQueryOperator.Equals, "true", FlexFieldValueType.Boolean));

        ids.ShouldBe(new[] { featured });
    }

    [Fact]
    public async Task In_operator_matches_a_multi_valued_field_on_any_of_its_values()
    {
        var tags = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        var tagged = await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "red", "blue" }));
        await IndexedArticleAsync(a => a.SetField("Tags", new List<string> { "green" }));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            tags.Field.Id, FlexFieldQueryOperator.In, "red,purple", FlexFieldValueType.String));

        // One entity, even though it has two rows for this field.
        ids.ShouldBe(new[] { tagged });
    }

    [Fact]
    public async Task No_match_returns_empty()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        await IndexedArticleAsync(a => a.SetField("Title", "Alpha"));

        var ids = await QueryAsync(new FlexFieldQueryCondition(
            title.Field.Id, FlexFieldQueryOperator.Equals, "Nonexistent", FlexFieldValueType.String));

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
            new FlexFieldQueryCondition(title.Field.Id, FlexFieldQueryOperator.Equals, "Nope", FlexFieldValueType.String),
            new FlexFieldQueryCondition(viewCount.Field.Id, FlexFieldQueryOperator.Equals, "20", FlexFieldValueType.Number));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task Number_conditions_parse_with_invariant_culture()
    {
        var viewCount = Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var matching = await IndexedArticleAsync(a => a.SetField("Price", 42.5m));

        // "42.5" must mean forty-two-and-a-half regardless of the ambient culture's decimal separator.
        var ids = await QueryAsync(new FlexFieldQueryCondition(
            viewCount.Field.Id, FlexFieldQueryOperator.Equals, "42.5", FlexFieldValueType.Number));

        ids.ShouldBe(new[] { matching });
    }

    [Fact]
    public async Task An_empty_condition_list_is_rejected()
    {
        await Should.ThrowAsync<ArgumentException>(() => WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            await Executor.ApplyFilterAsync(dbContext.Articles, Array.Empty<FlexFieldQueryCondition>());
        }));
    }

    private async Task<IReadOnlyList<Guid>> QueryAsync(params FlexFieldQueryCondition[] conditions)
    {
        IReadOnlyList<Guid> ids = Array.Empty<Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var filtered = await Executor.ApplyFilterAsync(dbContext.Articles, conditions);
            ids = await filtered.Select(a => a.Id).ToListAsync();
        });
        return ids;
    }

    /// <summary>Inserts a host entity and synchronizes its index rows.</summary>
    private async Task<Guid> IndexedArticleAsync(Action<TestArticle> configure)
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var article = new TestArticle(articleId, "Host");
            configure(article);

            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.AddAsync(article);
            await dbContext.SaveChangesAsync();

            await Manager.SynchronizeAsync(article);
        });

        return articleId;
    }

    private Task<ITestFlexFieldsDbContext> GetDbContextAsync()
    {
        return GetRequiredService<IDbContextProvider<ITestFlexFieldsDbContext>>().GetDbContextAsync();
    }
}
