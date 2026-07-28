using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// What actually lands in BSON for each kind of bag value, and what comes back out. This provider queries
/// the bag in place, so the stored representation is not an implementation detail - it is the thing every
/// filter compares against. A number stored as a string still matches an equality filter built from the same
/// string, and then silently fails every range query.
/// </summary>
public class FlexFieldsBsonRoundTrip_Tests : FlexFieldsMongoDbTestBase
{
    [Fact]
    public async Task Each_value_type_lands_in_a_natively_comparable_bson_type()
    {
        var guid = Guid.NewGuid();
        var articleId = await InsertArticleAsync(a =>
        {
            a.SetField("Text", "hello");
            a.SetField("Decimal", 42.5m);
            a.SetField("Double", 42.5d);
            a.SetField("Int", 42);
            a.SetField("Date", new DateTime(2025, 6, 1, 13, 45, 0, DateTimeKind.Utc));
            a.SetField("Flag", true);
            a.SetField("Reference", guid);
            a.SetField("Tags", new List<string> { "red", "blue" });
        });

        var bag = (await GetRawBagAsync(articleId)).AsBsonDocument;
        var actual = bag.Elements.ToDictionary(e => e.Name, e => e.Value.BsonType.ToString());

        actual.ShouldBe(new Dictionary<string, string>
        {
            ["Text"] = nameof(BsonType.String),
            ["Decimal"] = nameof(BsonType.Decimal128),
            ["Double"] = nameof(BsonType.Double),
            ["Int"] = nameof(BsonType.Int32),
            ["Date"] = nameof(BsonType.DateTime),
            ["Flag"] = nameof(BsonType.Boolean),
            ["Reference"] = nameof(BsonType.Binary),
            ["Tags"] = nameof(BsonType.Array)
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task A_string_list_comes_back_readable_by_a_multi_valued_field_type()
    {
        var articleId = await InsertArticleAsync(a => a.SetField("Tags", new List<string> { "red", "blue" }));

        var article = await GetArticleAsync(articleId);

        // Not necessarily a List<string>: the bag's value type is object, so the driver is free to hand back
        // boxed elements. FieldTypeBase.ReadStringList is what makes that shape readable - before it, the
        // multi-valued field types cast straight to List<string> and this threw.
        var fieldType = GetRequiredService<IFieldTypeResolver>().Get(SelectFieldType.ControlName);
        var field = new FlexFieldValue(
            new FlexFieldData(Guid.NewGuid(), "Tags", "Tags", SelectFieldType.ControlName),
            searchable: true,
            value: article.GetField("Tags"));

        fieldType.GetSearchableValues(field).ShouldBe(new object[] { "red", "blue" });
    }

    [Fact]
    public async Task Scalars_come_back_as_the_clr_types_they_went_in_as()
    {
        var guid = Guid.NewGuid();
        var date = new DateTime(2025, 6, 1, 13, 45, 0, DateTimeKind.Utc);
        var articleId = await InsertArticleAsync(a =>
        {
            a.SetField("Text", "hello");
            a.SetField("Decimal", 42.5m);
            a.SetField("Date", date);
            a.SetField("Flag", true);
            a.SetField("Reference", guid);
        });

        var article = await GetArticleAsync(articleId);

        article.GetField("Text").ShouldBe("hello");
        article.GetField("Decimal").ShouldBe(42.5m);
        article.GetField("Date").ShouldBe(date);
        article.GetField("Flag").ShouldBe(true);
        article.GetField("Reference").ShouldBe(guid);
    }

    private async Task<BsonValue> GetRawBagAsync(Guid articleId)
    {
        BsonValue bag = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();

            var document = await dbContext.Database
                .GetCollection<BsonDocument>("TestArticles")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", articleId))
                .SingleAsync();

            bag = document[nameof(IHasFlexFields.FlexFields)];
        });

        return bag;
    }
}
