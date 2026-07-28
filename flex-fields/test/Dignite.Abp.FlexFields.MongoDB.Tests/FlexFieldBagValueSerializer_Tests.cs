using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// <see cref="FlexFieldBagValueSerializer"/>'s two edge cases beyond the ordinary CLR-typed values covered by
/// <see cref="FlexFieldsBsonRoundTrip_Tests"/>: a <see cref="JsonElement"/> value (the shape
/// <see cref="FlexibleEntityDto"/> actually carries in from a request body) must not lose data, and widening
/// the fallback <c>ObjectSerializer</c>'s allowed types must not reopen a way for a caller-supplied
/// discriminator to drive type instantiation on read-back.
/// </summary>
public class FlexFieldBagValueSerializer_Tests : FlexFieldsMongoDbTestBase
{
    [Theory]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("42.5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[\"red\",\"blue\"]")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"Text\":\"Red\",\"Value\":\"red\",\"Selected\":false}")]
    public async Task A_JsonElement_value_round_trips_without_losing_its_content(string json)
    {
        // The exact shape a Dictionary<string, object>-typed DTO property holds after System.Text.Json model
        // binding - FlexibleEntityDto.FlexFields itself, most concretely.
        var element = JsonDocument.Parse(json).RootElement.Clone();

        var articleId = await InsertArticleAsync(a => a.SetField("FromDto", element));

        var stored = await GetRawBagFieldAsync(articleId, "FromDto");

        stored.ShouldBe(ToExpectedBson(element));
    }

    [Fact]
    public async Task A_caller_supplied_discriminator_key_does_not_drive_type_instantiation()
    {
        // Stands in for a bag value built from request input where the caller chose every key, including one
        // that happens to collide with the BSON discriminator convention's own field name.
        var attackerControlled = new Dictionary<string, object>
        {
            ["_t"] = "Dignite.Abp.FlexFields.MongoDB.TestField",
            ["Name"] = "not actually a TestField"
        };

        var articleId = await InsertArticleAsync(a => a.SetField("Evil", attackerControlled));

        var article = await GetArticleAsync(articleId);
        var readBack = article.GetField("Evil");

        // Not reconstructed as TestField (or anything else the caller named) - the caller's "_t" is ordinary
        // dictionary content, never consulted as a type discriminator.
        readBack.ShouldBeOfType<Dictionary<string, object>>();
    }

    /// <summary>
    /// Mirrors <c>FlexFieldBagValueSerializer.SerializeJsonElement</c>'s own type choice per
    /// <see cref="JsonValueKind"/> - independently, rather than by parsing the same JSON text through
    /// <see cref="BsonDocument.Parse"/>, whose numeric-type inference is a different, unrelated policy (it
    /// picks Int32 for a small whole number; the production code deliberately picks Int64, matching
    /// <c>FlexFieldValueConverter.Unwrap</c>'s reading of the same shape).
    /// </summary>
    private static BsonValue ToExpectedBson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => BsonNull.Value,
            JsonValueKind.String => new BsonString(element.GetString()),
            JsonValueKind.True => BsonBoolean.True,
            JsonValueKind.False => BsonBoolean.False,
            JsonValueKind.Number => element.TryGetInt64(out var integer)
                ? new BsonInt64(integer)
                : new BsonDecimal128(element.GetDecimal()),
            JsonValueKind.Array => new BsonArray(EnumerateExpected(element.EnumerateArray())),
            JsonValueKind.Object => ToExpectedDocument(element),
            _ => throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, null)
        };

        static IEnumerable<BsonValue> EnumerateExpected(JsonElement.ArrayEnumerator items)
        {
            foreach (var item in items)
            {
                yield return ToExpectedBson(item);
            }
        }

        static BsonDocument ToExpectedDocument(JsonElement obj)
        {
            var document = new BsonDocument();
            foreach (var property in obj.EnumerateObject())
            {
                document.Add(property.Name, ToExpectedBson(property.Value));
            }
            return document;
        }
    }

    private async Task<BsonValue> GetRawBagFieldAsync(Guid articleId, string fieldName)
    {
        BsonValue value = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var document = await dbContext.Database
                .GetCollection<BsonDocument>("TestArticles")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", articleId))
                .SingleAsync();

            value = document[nameof(IHasFlexFields.FlexFields)].AsBsonDocument[fieldName];
        });

        return value;
    }
}
