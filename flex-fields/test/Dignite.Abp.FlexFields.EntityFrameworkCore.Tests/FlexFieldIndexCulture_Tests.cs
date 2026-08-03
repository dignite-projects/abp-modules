using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Number;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// The index side and the query side must read a raw value the same way, or a value that was indexed can
/// no longer be found.
/// <para>
/// The query side parses <see cref="FlexFieldQueryCondition.Value"/> with the invariant culture and says so
/// - a condition is transport data, so it must not be read against whatever culture the request happens to
/// run under. The index side reaches the same value through <c>Convert.ToDecimal</c> /
/// <c>Convert.ToDateTime</c>, which use the <i>ambient</i> culture when handed a string. A bag value that
/// is a string (which is what a text-typed source, or a field whose type later changed to Number, leaves
/// behind) therefore goes in under one rule and is searched for under another.
/// </para>
/// <para>
/// <see cref="FlexFieldQueryExecutor_Tests.Number_conditions_parse_with_invariant_culture"/> does not catch
/// this: it never changes the ambient culture, and it puts a <c>decimal</c> in the bag rather than a string,
/// so the index side never parses anything.
/// </para>
/// </summary>
public class FlexFieldIndexCulture_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    /// <summary>A culture whose decimal separator is ',' and whose group separator is '.'.</summary>
    private const string CommaDecimalCulture = "de-DE";

    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldIndexManager<TestArticle> Manager => GetRequiredService<IFlexFieldIndexManager<TestArticle>>();

    private IFlexFieldQueryExecutor<TestArticle> Executor => GetRequiredService<IFlexFieldQueryExecutor<TestArticle>>();

    [Fact]
    public async Task A_decimal_string_is_indexed_by_invariant_culture()
    {
        Provider.AddDefinition("Price", NumberFieldType.ControlName);

        using (UseCulture(CommaDecimalCulture))
        {
            var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

            await WithUnitOfWorkAsync(async () =>
            {
                var row = (await GetRowsAsync(articleId)).ShouldHaveSingleItem();

                // "42.5" is transport data, not a localized number: it means forty-two-and-a-half whatever
                // the ambient culture's separators are. Read under de-DE, '.' is a group separator, so an
                // ambient-culture parse yields 425.
                row.NumberValue.ShouldBe(42.5m);
            });
        }
    }

    [Fact]
    public async Task A_decimal_string_stays_findable_under_a_comma_decimal_culture()
    {
        var price = Provider.AddDefinition("Price", NumberFieldType.ControlName);

        using (UseCulture(CommaDecimalCulture))
        {
            var articleId = await IndexedArticleAsync(a => a.SetField("Price", "42.5"));

            var ids = await QueryAsync(new FlexFieldQueryCondition(
                price.Field.Id, price.Field.Name, FlexFieldQueryOperator.Equals, "42.5", FlexFieldValueType.Number));

            ids.ShouldBe(new[] { articleId });
        }
    }

    [Fact]
    public async Task A_decimal_bag_value_is_unaffected_by_the_ambient_culture()
    {
        // The control: a value that is already a decimal never goes through a parse, so it indexes the same
        // under any culture. This is what pins the failure above to the string path specifically.
        Provider.AddDefinition("Price", NumberFieldType.ControlName);

        using (UseCulture(CommaDecimalCulture))
        {
            var articleId = await IndexedArticleAsync(a => a.SetField("Price", 42.5m));

            await WithUnitOfWorkAsync(async () =>
                (await GetRowsAsync(articleId)).ShouldHaveSingleItem().NumberValue.ShouldBe(42.5m));
        }
    }

    private static IDisposable UseCulture(string name)
    {
        return new CultureScope(name);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo? _previousDefaultCulture;

        public CultureScope(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);

            _previousCulture = CultureInfo.CurrentCulture;
            _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;

            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        }
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
