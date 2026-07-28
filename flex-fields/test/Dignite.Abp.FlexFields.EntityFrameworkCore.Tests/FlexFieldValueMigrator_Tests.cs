using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Text;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// End to end through <see cref="IFlexFieldValueMigrator{TEntity}"/>: renaming or deleting a field
/// definition, then rewriting every host entity's value bag so the values stay reachable.
/// <para>
/// The implementation under test is the kernel's provider-agnostic
/// <see cref="FlexFieldValueMigrator{TEntity}"/> - resolved straight off the open-generic registration,
/// with no downstream subclass, which is the point. It needs a real host entity, provider and database to
/// run against, so its tests live in this project rather than one of their own (the same reason
/// <c>FlexFieldValidator_Tests</c> does).
/// </para>
/// </summary>
public class FlexFieldValueMigrator_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldValueMigrator<TestArticle> Migrator => GetRequiredService<IFlexFieldValueMigrator<TestArticle>>();

    /// <summary>Same migrator, page size 2 - only the paging tests care.</summary>
    private SmallPageFlexFieldValueMigrator PagedMigrator => GetRequiredService<SmallPageFlexFieldValueMigrator>();

    private IFlexFieldIndexManager<TestArticle> Manager => GetRequiredService<IFlexFieldIndexManager<TestArticle>>();

    private IFlexFieldQueryExecutor<TestArticle> Executor => GetRequiredService<IFlexFieldQueryExecutor<TestArticle>>();

    private IFlexFieldValidator<TestArticle> Validator => GetRequiredService<IFlexFieldValidator<TestArticle>>();

    [Fact]
    public async Task Rename_moves_the_value_to_the_new_key_and_persists_it()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));

        (await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"))).ShouldBe(1);

        var article = await GetArticleAsync(articleId);
        article.HasField("AuthorName").ShouldBeFalse();
        article.GetField("Author").ShouldBe("Ada");
    }

    [Fact]
    public void The_kernel_registers_a_migrator_for_every_host_type_with_no_downstream_code()
    {
        // The open generic in FlexFieldsDomainModule is the whole adoption story - unlike
        // IFlexFieldIndexManager, no downstream subclass exists or is needed.
        Migrator.ShouldBeOfType<FlexFieldValueMigrator<TestArticle>>();
    }

    [Fact]
    public async Task Rename_covers_every_entity_across_multiple_pages()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);

        // PagedMigrator's page size is 2, so 5 entities span 3 pages (2 + 2 + 1).
        var articleIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            articleIds.Add(await InsertArticleAsync(a => a.SetField("AuthorName", $"Author {i}")));
        }

        (await MigrateAsync(() => PagedMigrator.RenameFieldAsync("AuthorName", "Author"))).ShouldBe(5);

        foreach (var articleId in articleIds)
        {
            var article = await GetArticleAsync(articleId);
            article.HasField("AuthorName").ShouldBeFalse();
            article.GetField("Author").ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Rename_on_an_exact_page_multiple_terminates()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);

        // Exactly two pages of two - the loop must stop on the empty third page, not spin.
        for (var i = 0; i < 4; i++)
        {
            await InsertArticleAsync(a => a.SetField("AuthorName", $"Author {i}"));
        }

        (await MigrateAsync(() => PagedMigrator.RenameFieldAsync("AuthorName", "Author"))).ShouldBe(4);
    }

    [Fact]
    public async Task Rerunning_a_rename_changes_nothing()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));

        await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"));

        // Idempotent, which is what makes a partially-failed paged migration safe to re-run.
        (await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"))).ShouldBe(0);
        (await GetArticleAsync(articleId)).GetField("Author").ShouldBe("Ada");
    }

    [Fact]
    public async Task Rename_leaves_entities_that_never_held_the_field_alone()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var withField = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));
        var withoutField = await InsertArticleAsync(a => a.SetField("ViewCount", 42));

        // Only the one entity counts, even though both were visited.
        (await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"))).ShouldBe(1);

        (await GetArticleAsync(withField)).GetField("Author").ShouldBe("Ada");

        var untouched = await GetArticleAsync(withoutField);
        untouched.HasField("Author").ShouldBeFalse();
        untouched.FlexFields.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Rename_moves_only_the_named_key_on_an_entity_holding_several()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var articleId = await InsertArticleAsync(a =>
        {
            a.SetField("AuthorName", "Ada");
            a.SetField("ViewCount", 42);
        });

        await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"));

        var article = await GetArticleAsync(articleId);
        article.GetField("Author").ShouldBe("Ada");
        article.GetField<int>("ViewCount").ShouldBe(42);
        article.FlexFields.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Renaming_onto_a_key_that_is_already_in_use_throws()
    {
        // The caller was meant to rule this out with IFlexFieldRepository.NameExistsAsync first.
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        Provider.AddDefinition("Author", TextFieldType.ControlName);

        await InsertArticleAsync(a =>
        {
            a.SetField("AuthorName", "Ada");
            a.SetField("Author", "Grace");
        });

        await Should.ThrowAsync<InvalidOperationException>(
            MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author")));
    }

    [Fact]
    public async Task Remove_drops_the_key_and_leaves_the_others()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var articleId = await InsertArticleAsync(a =>
        {
            a.SetField("AuthorName", "Ada");
            a.SetField("ViewCount", 42);
        });

        (await MigrateAsync(() => Migrator.RemoveFieldAsync("AuthorName"))).ShouldBe(1);

        var article = await GetArticleAsync(articleId);
        article.HasField("AuthorName").ShouldBeFalse();
        article.GetField<int>("ViewCount").ShouldBe(42);
    }

    [Fact]
    public async Task Rerunning_a_remove_changes_nothing()
    {
        Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));

        await MigrateAsync(() => Migrator.RemoveFieldAsync("AuthorName"));

        (await MigrateAsync(() => Migrator.RemoveFieldAsync("AuthorName"))).ShouldBe(0);
    }

    /// <summary>
    /// The failure this whole mechanism exists to prevent. Renaming the definition alone leaves the value
    /// under the old key, so the next SynchronizeAsync projects nothing and ReplaceRowsAsync deletes the
    /// index rows that were there - the field silently drops out of query results.
    /// </summary>
    [Fact]
    public async Task Renaming_a_definition_without_migrating_loses_the_index_rows()
    {
        var usage = Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));
        await SynchronizeAsync(articleId);

        (await QueryAsync(usage.Field.Id, "Ada")).ShouldBe(new[] { articleId });

        usage.Field.Name = "Author";
        await SynchronizeAsync(articleId);

        (await QueryAsync(usage.Field.Id, "Ada")).ShouldBeEmpty();
    }

    /// <summary>
    /// The same sequence with the migration in its documented place - the value survives the rename and
    /// stays queryable. Note the index itself never needs rebuilding: its rows key on field id and value,
    /// neither of which a rename touches.
    /// </summary>
    [Fact]
    public async Task Migrating_after_a_rename_keeps_the_field_queryable()
    {
        var usage = Provider.AddDefinition("AuthorName", TextFieldType.ControlName);
        var articleId = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));
        await SynchronizeAsync(articleId);

        usage.Field.Name = "Author";
        await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"));
        await SynchronizeAsync(articleId);

        (await QueryAsync(usage.Field.Id, "Ada")).ShouldBe(new[] { articleId });
    }

    /// <summary>
    /// The other silent symptom: a renamed required field reads back as null, so validation reports the
    /// user's own filled-in value as missing. Migrating clears it.
    /// </summary>
    [Fact]
    public async Task Migrating_after_a_rename_stops_required_validation_misfiring()
    {
        var usage = Provider.AddDefinition("AuthorName", TextFieldType.ControlName, required: true);
        var articleId = await InsertArticleAsync(a => a.SetField("AuthorName", "Ada"));

        usage.Field.Name = "Author";
        var error = (await Validator.ValidateAsync(await GetArticleAsync(articleId))).ShouldHaveSingleItem();
        error.MemberNames.ShouldBe(new[] { "Author" });

        await MigrateAsync(() => Migrator.RenameFieldAsync("AuthorName", "Author"));

        (await Validator.ValidateAsync(await GetArticleAsync(articleId))).ShouldBeEmpty();
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

    private async Task<int> MigrateAsync(Func<Task<int>> migrate)
    {
        var changedCount = 0;
        await WithUnitOfWorkAsync(async () => changedCount = await migrate());
        return changedCount;
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

    /// <summary>Re-reads from the database, so an assertion cannot pass on a stale in-memory bag.</summary>
    private async Task<TestArticle> GetArticleAsync(Guid articleId)
    {
        TestArticle? article = null;
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            article = await dbContext.Articles.AsNoTracking().SingleAsync(x => x.Id == articleId);
        });
        return article!;
    }

    private async Task<IReadOnlyList<Guid>> QueryAsync(Guid fieldId, string value)
    {
        IReadOnlyList<Guid> ids = Array.Empty<Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var filtered = await Executor.ApplyFilterAsync(
                dbContext.Articles,
                new[]
                {
                    new FlexFieldQueryCondition(
                        fieldId, FlexFieldQueryOperator.Equals, value, FlexFieldValueType.String)
                });
            ids = await filtered.Select(a => a.Id).ToListAsync();
        });
        return ids;
    }

    private Task<ITestFlexFieldsDbContext> GetDbContextAsync()
    {
        return GetRequiredService<IDbContextProvider<ITestFlexFieldsDbContext>>().GetDbContextAsync();
    }
}
