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
/// End to end through <see cref="IFlexFieldIndexManager{TEntity}"/>: host entity -&gt;
/// <see cref="IFlexFieldProvider{TEntity}"/> -&gt; <see cref="IFieldType.GetSearchableValues"/> -&gt; the
/// downstream's index table.
/// </summary>
public class FlexFieldIndexManager_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldIndexManager<TestArticle> Manager => GetRequiredService<IFlexFieldIndexManager<TestArticle>>();

    [Fact]
    public async Task Synchronize_projects_every_built_in_field_type_and_skips_non_searchable_usages()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var viewCount = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);
        var publishedAt = Provider.AddDefinition("PublishedAt", DateTimeFieldType.ControlName);
        var isFeatured = Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName);
        var tags = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        var notes = Provider.AddDefinition("Notes", TextFieldType.ControlName, searchable: false);

        var articleId = await InsertArticleAsync(article =>
        {
            article.SetField("Title", "Hello world");
            article.SetField("ViewCount", 123);
            article.SetField("PublishedAt", new DateTime(2025, 6, 1));
            article.SetField("IsFeatured", true);
            article.SetField("Tags", new List<string> { "red", "blue" });
            article.SetField("Notes", "must not be indexed");
        });

        await SynchronizeAsync(articleId);

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await GetRowsAsync(articleId);

            // Title, ViewCount, PublishedAt, IsFeatured, 2x Tags - Notes excluded.
            rows.Count.ShouldBe(6);
            rows.Single(r => r.FieldId == title.Field.Id).StringValue.ShouldBe("Hello world");
            rows.Single(r => r.FieldId == viewCount.Field.Id).NumberValue.ShouldBe(123m);
            rows.Single(r => r.FieldId == publishedAt.Field.Id).DateTimeValue.ShouldBe(new DateTime(2025, 6, 1));
            rows.Single(r => r.FieldId == isFeatured.Field.Id).BooleanValue.ShouldBe(true);
            rows.Where(r => r.FieldId == tags.Field.Id).Select(r => r.StringValue)
                .ShouldBe(new[] { "red", "blue" }, ignoreOrder: true);
            rows.Any(r => r.FieldId == notes.Field.Id).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Synchronize_skips_a_searchable_usage_whose_field_type_is_not_indexable()
    {
        // NonIndexableFieldType.GetSearchableValues does NOT self-guard on IndexValueType (unlike
        // FieldTypeBase's default implementation), so this specifically exercises ProjectAsync's own
        // `if (fieldType.IndexValueType == null) { continue; }` guard - without it, dereferencing
        // fieldType.IndexValueType.Value below would throw.
        Provider.AddDefinition("Notes", NonIndexableFieldType.ControlName, searchable: true);
        var articleId = await InsertArticleAsync(a => a.SetField("Notes", "free text"));

        await SynchronizeAsync(articleId);

        await WithUnitOfWorkAsync(async () => (await GetRowsAsync(articleId)).ShouldBeEmpty());
    }

    [Fact]
    public async Task Synchronize_replaces_the_entitys_rows_rather_than_appending()
    {
        var title = Provider.AddDefinition("Title", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Title", "Old"));

        await SynchronizeAsync(articleId);

        // Change the value, then re-synchronize.
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);
            article.SetField("Title", "New");
            await Manager.SynchronizeAsync(article);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var row = (await GetRowsAsync(articleId)).ShouldHaveSingleItem();
            row.FieldId.ShouldBe(title.Field.Id);
            row.StringValue.ShouldBe("New");
        });
    }

    [Fact]
    public async Task Synchronize_clears_rows_when_a_value_is_removed()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Title", "Something"));

        await SynchronizeAsync(articleId);
        await WithUnitOfWorkAsync(async () => (await GetRowsAsync(articleId)).ShouldNotBeEmpty());

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);
            article.RemoveField("Title");
            await Manager.SynchronizeAsync(article);
        });

        await WithUnitOfWorkAsync(async () => (await GetRowsAsync(articleId)).ShouldBeEmpty());
    }

    [Fact]
    public async Task Synchronize_leaves_other_entities_rows_untouched()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName);
        var mineId = await InsertArticleAsync(a => a.SetField("Title", "Mine"));
        var theirsId = await InsertArticleAsync(a => a.SetField("Title", "Theirs"));

        await SynchronizeAsync(mineId);
        await SynchronizeAsync(theirsId);

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == mineId);
            article.RemoveField("Title");
            await Manager.SynchronizeAsync(article);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRowsAsync(mineId)).ShouldBeEmpty();
            (await GetRowsAsync(theirsId)).ShouldHaveSingleItem().StringValue.ShouldBe("Theirs");
        });
    }

    [Fact]
    public async Task Rebuild_backfills_every_entity_across_multiple_pages()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName);

        // RebuildPageSize is 2 in the test manager, so 5 entities span 3 pages (2 + 2 + 1).
        var articleIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("Title", $"Article {i}")));
        }

        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var articleId in articleIds)
            {
                (await GetRowsAsync(articleId)).ShouldHaveSingleItem().FieldId.ShouldNotBe(Guid.Empty);
            }
        });
    }

    [Fact]
    public async Task Rebuild_on_an_exact_page_multiple_terminates()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName);

        // Exactly two pages of two - the loop must stop on the empty third page, not spin.
        var articleIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("Title", $"Article {i}")));
        }

        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var articleId in articleIds)
            {
                (await GetRowsAsync(articleId)).Count.ShouldBe(1);
            }
        });
    }

    [Fact]
    public async Task Rebuild_picks_up_a_field_whose_searchable_flag_was_just_switched_on()
    {
        // Reindex trigger #1: Searchable flips on, value type unchanged.
        var usage = Provider.AddDefinition("Title", TextFieldType.ControlName, searchable: false);
        var articleId = await InsertArticleAsync(a => a.SetField("Title", "Not yet searchable"));

        await SynchronizeAsync(articleId);
        await WithUnitOfWorkAsync(async () => (await GetRowsAsync(articleId)).ShouldBeEmpty());

        usage.Searchable = true;
        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        await WithUnitOfWorkAsync(async () =>
            (await GetRowsAsync(articleId)).ShouldHaveSingleItem().StringValue.ShouldBe("Not yet searchable"));
    }

    [Fact]
    public async Task Rebuild_moves_a_value_to_a_different_slot_when_the_fields_type_changes()
    {
        // Reindex trigger #2: the control's type changes, so the value projects into another slot.
        // Project converts from the raw bag value on every run, which is why re-projecting is a
        // complete migration and no separate value migrator is needed.
        var usage = Provider.AddDefinition("Price", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Price", "42.5"));

        await SynchronizeAsync(articleId);

        await WithUnitOfWorkAsync(async () =>
        {
            var row = (await GetRowsAsync(articleId)).ShouldHaveSingleItem();
            row.ValueType.ShouldBe(FlexFieldValueType.String);
            row.StringValue.ShouldBe("42.5");
            row.NumberValue.ShouldBeNull();
        });

        usage.Field.FieldTypeName = NumberFieldType.ControlName;
        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            var row = (await GetRowsAsync(articleId)).ShouldHaveSingleItem();
            row.ValueType.ShouldBe(FlexFieldValueType.Number);
            row.NumberValue.ShouldBe(42.5m);
            row.StringValue.ShouldBeNull();
        });
    }

    private async Task<Guid> InsertArticleAsync(Action<TestArticle> configure)
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var article = new TestArticle(articleId, "Host");
            configure(article);

            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.AddAsync(article);
            await dbContext.SaveChangesAsync();
        });

        return articleId;
    }

    private Task SynchronizeAsync(Guid articleId)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);
            await Manager.SynchronizeAsync(article);
        });
    }

    private async Task<List<TestArticleFlexFieldIndex>> GetRowsAsync(Guid articleId)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.ArticleFlexFieldIndexes.Where(x => x.ArticleId == articleId).ToListAsync();
    }

    private Task<ITestFlexFieldsDbContext> GetDbContextAsync()
    {
        return GetRequiredService<IDbContextProvider<ITestFlexFieldsDbContext>>().GetDbContextAsync();
    }
}
