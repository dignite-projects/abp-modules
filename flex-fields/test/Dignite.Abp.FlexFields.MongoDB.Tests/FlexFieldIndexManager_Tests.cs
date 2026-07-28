using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Switch;
using Dignite.Abp.FlexFields.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// This provider has no pivot table, so its index manager's job is not to write derived rows but to leave
/// the bag in a form a native path filter can address. The relational provider gets that guarantee from its
/// typed columns; here it has to be established in the bag itself.
/// </summary>
public class FlexFieldIndexManager_Tests : FlexFieldsMongoDbTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldIndexManager<TestArticle> Manager => GetRequiredService<IFlexFieldIndexManager<TestArticle>>();

    [Fact]
    public async Task Synchronize_reads_a_string_into_the_type_its_field_names()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        Provider.AddDefinition("PublishedAt", DateTimeFieldType.ControlName);
        Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName);

        var articleId = await InsertArticleAsync(a =>
        {
            a.SetField("Price", "42.5");
            a.SetField("PublishedAt", "2025-06-01T13:45:00");
            a.SetField("IsFeatured", "true");
        });

        await SynchronizeAsync(articleId);

        var article = await GetArticleAsync(articleId);
        article.GetField("Price").ShouldBe(42.5m);
        // Read back as UTC, not shifted by the server's timezone - see FlexFieldBagValueSerializer.ToUtc.
        article.GetField("PublishedAt").ShouldBe(new DateTime(2025, 6, 1, 13, 45, 0, DateTimeKind.Utc));
        article.GetField("IsFeatured").ShouldBe(true);
    }

    [Fact]
    public async Task A_synchronized_number_is_stored_as_a_bson_number()
    {
        // The point of the whole exercise: "42.5" as a BSON string is reachable by an equality filter built
        // from the same string and by no range filter at all.
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Price", "42.5"));

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.String);

        await SynchronizeAsync(articleId);

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.Decimal128);
    }

    [Fact]
    public async Task Synchronize_writes_nothing_when_every_value_is_already_in_its_indexable_form()
    {
        // The near-zero write-time synchronization the document provider is supposed to have: a host that
        // writes well-typed values pays for none of this. Observed through the concurrency stamp, which an
        // ABP update rewrites.
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Price", 42.5m));

        var stampAfterFirst = (await GetArticleAsync(articleId)).ConcurrencyStamp;

        await SynchronizeAsync(articleId);

        (await GetArticleAsync(articleId)).ConcurrencyStamp.ShouldBe(stampAfterFirst);
    }

    [Fact]
    public async Task Synchronize_is_idempotent()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        await SynchronizeAsync(articleId);
        await SynchronizeAsync(articleId);

        (await GetArticleAsync(articleId)).GetField("Price").ShouldBe(42.5m);
    }

    [Fact]
    public async Task Synchronize_leaves_a_non_searchable_usage_alone()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName, searchable: false);
        var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        // Untouched, so still a string: this provider can still find it, but without the type guarantee
        // that only an indexed field gets.
        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.String);
    }

    [Fact]
    public async Task Synchronize_skips_a_searchable_usage_whose_field_type_is_not_indexable()
    {
        // NonIndexableFieldType does not self-guard on IndexValueType, so this exercises
        // FlexFieldIndexManagerBase's own guard - without it, reading IndexValueType.Value would throw.
        Provider.AddDefinition("Notes", NonIndexableFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Notes", "free text"));

        (await GetArticleAsync(articleId)).GetField("Notes").ShouldBe("free text");
    }

    [Fact]
    public async Task Synchronize_leaves_a_field_with_no_value_alone()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Title", "no price here"));

        (await GetArticleAsync(articleId)).HasField("Price").ShouldBeFalse();
    }

    [Fact]
    public async Task Rebuild_backfills_every_entity_across_multiple_pages()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);

        // RebuildPageSize is 2 in the test manager, so 5 entities span 3 pages (2 + 2 + 1).
        var articleIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("Price", $"{i}.5")));
        }

        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        foreach (var articleId in articleIds)
        {
            (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.Decimal128);
        }
    }

    [Fact]
    public async Task Rebuild_on_an_exact_page_multiple_terminates()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);

        // Exactly two pages of two - the loop must stop on the empty third page, not spin.
        var articleIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("Price", $"{i}.5")));
        }

        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        foreach (var articleId in articleIds)
        {
            (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.Decimal128);
        }
    }

    [Fact]
    public async Task Rebuild_picks_up_a_field_whose_searchable_flag_was_just_switched_on()
    {
        // Reindex trigger #1, and it means the same thing here as under EF Core because the eligibility rule
        // is the shared one - not because both providers happen to agree.
        var usage = Provider.AddDefinition("Price", NumberFieldType.ControlName, searchable: false);
        var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.String);

        usage.Searchable = true;
        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.Decimal128);
    }

    [Fact]
    public async Task Rebuild_re_reads_a_value_when_the_fields_type_changes()
    {
        // Reindex trigger #2: the field's type changes, so the same raw text belongs in a different form.
        var usage = Provider.AddDefinition("Price", TextFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.String);

        usage.Field.FieldTypeName = NumberFieldType.ControlName;
        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        (await GetBagTypeAsync(articleId, "Price")).ShouldBe(BsonType.Decimal128);
    }

    [Fact]
    public async Task Rebuild_ensures_the_wildcard_index_exists()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        await InsertArticleAsync(a => a.SetField("Price", "42.5"));

        await WithUnitOfWorkAsync(() => Manager.RebuildAsync());

        (await GetIndexKeysAsync()).ShouldContain($"{nameof(IHasFlexFields.FlexFields)}.$**");
    }

    private async Task<BsonType> GetBagTypeAsync(Guid articleId, string fieldName)
    {
        BsonType bsonType = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var document = await dbContext.Database
                .GetCollection<BsonDocument>("TestArticles")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", articleId))
                .SingleAsync();

            bsonType = document[nameof(IHasFlexFields.FlexFields)].AsBsonDocument[fieldName].BsonType;
        });

        return bsonType;
    }

    private async Task<List<string>> GetIndexKeysAsync()
    {
        var keys = new List<string>();

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var indexes = await (await dbContext.Articles.Indexes.ListAsync()).ToListAsync();

            keys = indexes
                .SelectMany(index => index["key"].AsBsonDocument.Names)
                .ToList();
        });

        return keys;
    }
}
