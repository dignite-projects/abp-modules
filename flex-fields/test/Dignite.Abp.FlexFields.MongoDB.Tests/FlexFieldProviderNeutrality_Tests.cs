using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// The kernel ships one <see cref="IFlexFieldValueMigrator{TEntity}"/> and one
/// <see cref="IFlexFieldValidator{TEntity}"/>, both registered as open generics in
/// <c>FlexFieldsDomainModule</c>, and this package ships no counterpart of either. That is a claim about how
/// they behave on a document store, so it is worth checking on one rather than taking on faith - especially
/// the migrator, whose <c>UpdateAsync(autoSave: false)</c> means something different here: ABP's MongoDB
/// repository writes each entity immediately rather than batching, so a page is several round trips and is
/// not atomic.
/// </summary>
public class FlexFieldProviderNeutrality_Tests : FlexFieldsMongoDbTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldValueMigrator<TestArticle> Migrator =>
        GetRequiredService<IFlexFieldValueMigrator<TestArticle>>();

    private SmallPageFlexFieldValueMigrator PagingMigrator =>
        GetRequiredService<SmallPageFlexFieldValueMigrator>();

    private IFlexFieldValidator<TestArticle> Validator => GetRequiredService<IFlexFieldValidator<TestArticle>>();

    [Fact]
    public void The_kernel_registers_a_migrator_and_a_validator_with_no_downstream_code()
    {
        // Neither this package nor the test project defines either type for TestArticle.
        Migrator.ShouldNotBeNull();
        Validator.ShouldNotBeNull();
    }

    [Fact]
    public async Task Renaming_a_field_rewrites_the_bag_key()
    {
        Provider.AddDefinition("Headline", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Headline", "Hello"));

        var changed = await WithResultAsync(() => Migrator.RenameFieldAsync("Headline", "Title"));

        changed.ShouldBe(1);
        var article = await GetArticleAsync(articleId);
        article.HasField("Headline").ShouldBeFalse();
        article.GetField("Title").ShouldBe("Hello");
    }

    [Fact]
    public async Task Renaming_crosses_page_boundaries()
    {
        Provider.AddDefinition("Headline", TextFieldType.ControlName);

        // MigrationPageSize is 2 in the paging migrator, so 5 entities span 3 pages.
        var articleIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("Headline", $"Article {i}")));
        }

        var changed = await WithResultAsync(() => PagingMigrator.RenameFieldAsync("Headline", "Title"));

        changed.ShouldBe(5);
        foreach (var articleId in articleIds)
        {
            (await GetArticleAsync(articleId)).HasField("Title").ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Renaming_is_idempotent()
    {
        Provider.AddDefinition("Headline", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Headline", "Hello"));

        await WithResultAsync(() => Migrator.RenameFieldAsync("Headline", "Title"));
        var secondRun = await WithResultAsync(() => Migrator.RenameFieldAsync("Headline", "Title"));

        // Nothing left to move, so nothing is written - which is what makes a re-run after a partial failure
        // safe, and it holds here even though this provider writes per entity rather than per page.
        secondRun.ShouldBe(0);
        (await GetArticleAsync(articleId)).GetField("Title").ShouldBe("Hello");
    }

    [Fact]
    public async Task Renaming_leaves_hosts_that_never_held_the_key_alone()
    {
        Provider.AddDefinition("Headline", TextFieldType.ControlName);
        var untouchedId = await InsertArticleAsync(a => a.SetField("Unrelated", "no headline"));

        var changed = await WithResultAsync(() => Migrator.RenameFieldAsync("Headline", "Title"));

        changed.ShouldBe(0);
        (await GetArticleAsync(untouchedId)).GetField("Unrelated").ShouldBe("no headline");
    }

    [Fact]
    public async Task Removing_a_field_drops_its_key()
    {
        Provider.AddDefinition("Headline", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("Headline", "Hello"));

        var changed = await WithResultAsync(() => Migrator.RemoveFieldAsync("Headline"));

        changed.ShouldBe(1);
        (await GetArticleAsync(articleId)).HasField("Headline").ShouldBeFalse();
    }

    [Fact]
    public async Task A_rename_keeps_a_value_queryable()
    {
        // The end-to-end reason the migrator exists: this provider addresses the bag by name, so a rename
        // that did not rewrite the bag would leave the value unreachable under either name.
        var usage = Provider.AddDefinition("Headline", TextFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a => a.SetField("Headline", "Hello"));

        await WithResultAsync(() => Migrator.RenameFieldAsync("Headline", "Title"));
        usage.Field.Name = "Title";

        var ids = await QueryAsync(Condition(usage, FlexFieldQueryOperator.Equals, "Hello", FlexFieldValueType.String));

        ids.ShouldBe(new[] { articleId });
    }

    [Fact]
    public async Task Validation_reports_a_missing_required_value()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: true);
        var articleId = await InsertArticleAsync(a => a.SetField("Unrelated", "x"));

        var article = await GetArticleAsync(articleId);
        var errors = await Validator.ValidateAsync(article);

        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Validation_passes_a_well_formed_host()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: true);
        Provider.AddDefinition("Price", NumberFieldType.ControlName);
        var articleId = await IndexedArticleAsync(a =>
        {
            a.SetField("Title", "Hello");
            a.SetField("Price", "42.5");
        });

        var article = await GetArticleAsync(articleId);
        var errors = await Validator.ValidateAsync(article);

        // Also covers the value having been rewritten to a decimal by then - validation reads the bag after
        // synchronization, so a field type has to cope with the indexable form as well as the raw one.
        errors.ShouldBeEmpty();
    }

    private async Task<IReadOnlyList<Guid>> QueryAsync(params FlexFieldQueryCondition[] conditions)
    {
        IReadOnlyList<Guid> ids = Array.Empty<Guid>();

        await WithUnitOfWorkAsync(async () =>
        {
            var queryable = await GetRequiredService<IRepository<TestArticle, Guid>>().GetQueryableAsync();
            var filtered = await GetRequiredService<IFlexFieldQueryExecutor<TestArticle>>()
                .ApplyFilterAsync(queryable, conditions);
            ids = filtered.Select(a => a.Id).ToList();
        });

        return ids;
    }

    private async Task<TResult> WithResultAsync<TResult>(Func<Task<TResult>> action)
    {
        TResult result = default!;
        await WithUnitOfWorkAsync(async () => result = await action());
        return result;
    }
}
