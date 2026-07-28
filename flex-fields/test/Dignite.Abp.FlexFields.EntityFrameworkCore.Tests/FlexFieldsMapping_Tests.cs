using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Text;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Data round-trips through the columns the three kernel <c>EntityTypeBuilder</c> extensions map, against
/// a real (in-memory Sqlite) database. Model/schema shape itself is asserted in
/// <see cref="FlexFieldsModel_Tests"/>.
/// </summary>
public class FlexFieldsMapping_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task Value_bag_round_trips_through_its_json_column()
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var article = new TestArticle(articleId, "Hello");
            article.SetField("Title", "A title");
            article.SetField("ViewCount", 42);
            article.SetField("IsFeatured", true);
            article.SetField("Tags", new List<string> { "red", "blue" });

            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.AddAsync(article);
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);

            article.FlexFields.Count.ShouldBe(4);
            // GetField<T> unwraps the JsonElement boxing a Dictionary<string, object> round trip causes.
            article.GetField<string>("Title").ShouldBe("A title");
            article.GetField<int>("ViewCount").ShouldBe(42);
            article.GetField<bool>("IsFeatured").ShouldBeTrue();
            article.GetField<List<string>>("Tags").ShouldBe(new[] { "red", "blue" });
        });
    }

    [Fact]
    public async Task Removing_a_field_from_the_bag_persists_as_a_removal()
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var article = new TestArticle(articleId, "Hello");
            article.SetField("Title", "A title");
            article.SetField("Doomed", "bye");

            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.AddAsync(article);
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);
            article.RemoveField("Doomed");
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.SingleAsync(x => x.Id == articleId);

            article.HasField("Doomed").ShouldBeFalse();
            article.GetField<string>("Title").ShouldBe("A title");
        });
    }

    [Fact]
    public async Task Field_definition_columns_round_trip_including_description_and_configuration()
    {
        var fieldId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var field = new TestField(fieldId, "Title", TextFieldType.ControlName, "The article headline");
            _ = new TextConfiguration(field.Configuration)
            {
                Mode = TextMode.MultipleLine,
                CharLimit = 200,
                Placeholder = "Type a headline"
            };

            var dbContext = await GetDbContextAsync();
            await dbContext.Fields.AddAsync(field);
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var field = await dbContext.Fields.SingleAsync(x => x.Id == fieldId);

            field.Name.ShouldBe("Title");
            field.DisplayName.ShouldBe("Title");
            field.Description.ShouldBe("The article headline");
            field.FieldTypeName.ShouldBe(TextFieldType.ControlName);

            var configuration = new TextConfiguration(field.Configuration);
            configuration.Mode.ShouldBe(TextMode.MultipleLine);
            configuration.CharLimit.ShouldBe(200);
            configuration.Placeholder.ShouldBe("Type a headline");
        });
    }

    [Fact]
    public async Task Null_description_round_trips_as_null()
    {
        var fieldId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            await dbContext.Fields.AddAsync(new TestField(fieldId, "NoDescription", TextFieldType.ControlName));
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            (await dbContext.Fields.SingleAsync(x => x.Id == fieldId)).Description.ShouldBeNull();
        });
    }

    [Theory]
    [InlineData(FlexFieldValueType.String)]
    [InlineData(FlexFieldValueType.Number)]
    [InlineData(FlexFieldValueType.DateTime)]
    [InlineData(FlexFieldValueType.Boolean)]
    [InlineData(FlexFieldValueType.Guid)]
    public async Task Index_row_keeps_type_fidelity_for_every_value_slot(FlexFieldValueType valueType)
    {
        var articleId = await InsertArticleAsync();
        var fieldId = Guid.NewGuid();
        var rowId = Guid.NewGuid();

        var guidValue = Guid.NewGuid();
        var dateTimeValue = new DateTime(2025, 6, 1, 13, 45, 0);
        object raw = valueType switch
        {
            FlexFieldValueType.String => "hello",
            FlexFieldValueType.Number => 42.5m,
            FlexFieldValueType.DateTime => dateTimeValue,
            FlexFieldValueType.Boolean => true,
            FlexFieldValueType.Guid => guidValue,
            _ => throw new ArgumentOutOfRangeException(nameof(valueType))
        };

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            await dbContext.ArticleFlexFieldIndexes.AddAsync(new TestArticleFlexFieldIndex(
                rowId, articleId, fieldId, FlexFieldIndexValue.Create(valueType, raw)));
            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var row = await dbContext.ArticleFlexFieldIndexes.SingleAsync(x => x.Id == rowId);

            row.ArticleId.ShouldBe(articleId);
            row.FieldId.ShouldBe(fieldId);
            row.ValueType.ShouldBe(valueType);

            // Exactly the slot named by ValueType is populated - a number never lands as a string.
            switch (valueType)
            {
                case FlexFieldValueType.String:
                    row.StringValue.ShouldBe("hello");
                    break;
                case FlexFieldValueType.Number:
                    row.NumberValue.ShouldBe(42.5m);
                    break;
                case FlexFieldValueType.DateTime:
                    row.DateTimeValue.ShouldBe(dateTimeValue);
                    break;
                case FlexFieldValueType.Boolean:
                    row.BooleanValue.ShouldBe(true);
                    break;
                case FlexFieldValueType.Guid:
                    row.GuidValue.ShouldBe(guidValue);
                    break;
            }

            if (valueType != FlexFieldValueType.String)
            {
                row.StringValue.ShouldBeNull();
            }

            if (valueType != FlexFieldValueType.Number)
            {
                row.NumberValue.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task Multi_valued_projection_persists_as_several_index_rows_queryable_in_sql()
    {
        var articleId = await InsertArticleAsync();
        var fieldId = Guid.NewGuid();

        // The point of the index: a Select field's values become rows a WHERE clause can hit.
        var fieldType = GetRequiredService<IFieldTypeResolver>().Get(SelectFieldType.ControlName);
        var definition = new TestField(fieldId, "Tags", SelectFieldType.ControlName);
        var searchableValues = fieldType
            .GetSearchableValues(new FlexFieldValue(definition.ToFlexFieldData(), searchable: true, value: new List<string> { "red", "blue" }))
            .ToList();
        searchableValues.Count.ShouldBe(2);

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            foreach (var value in searchableValues)
            {
                await dbContext.ArticleFlexFieldIndexes.AddAsync(
                    new TestArticleFlexFieldIndex(
                        Guid.NewGuid(), articleId, fieldId,
                        FlexFieldIndexValue.Create(fieldType.IndexValueType!.Value, value)));
            }

            await dbContext.SaveChangesAsync();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();

            var matchedIds = await dbContext.ArticleFlexFieldIndexes
                .Where(x => x.FieldId == fieldId && x.StringValue == "blue")
                .Select(x => x.ArticleId)
                .Distinct()
                .ToListAsync();

            matchedIds.ShouldBe(new[] { articleId });

            (await dbContext.ArticleFlexFieldIndexes.CountAsync(x => x.ArticleId == articleId)).ShouldBe(2);
        });
    }

    private async Task<Guid> InsertArticleAsync()
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.AddAsync(new TestArticle(articleId, "Host"));
            await dbContext.SaveChangesAsync();
        });

        return articleId;
    }

    private Task<ITestFlexFieldsDbContext> GetDbContextAsync()
    {
        return GetRequiredService<IDbContextProvider<ITestFlexFieldsDbContext>>().GetDbContextAsync();
    }
}
