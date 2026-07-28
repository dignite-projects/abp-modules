using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Downstream stand-in for <see cref="IFlexFieldProvider{TEntity}"/>. Reads definitions from the mutable
/// <see cref="Definitions"/> list (standing in for a CMS's Field/EntryType lookup) and values from the
/// entity's own bag - and, for enumeration, from its own DbContext, never from the kernel's index table.
/// <para>
/// Singleton so a test can mutate <see cref="Definitions"/> (e.g. flip Searchable) and have the change
/// visible to the manager resolved in a later unit of work.
/// </para>
/// </summary>
public class TestArticleFlexFieldProvider : IFlexFieldProvider<TestArticle>, ISingletonDependency
{
    /// <summary>Mutable per-usage field metadata: definition + Required/Searchable.</summary>
    public List<TestArticleFieldUsage> Definitions { get; } = new();

    protected IDbContextProvider<ITestFlexFieldsDbContext> DbContextProvider { get; }

    public TestArticleFlexFieldProvider(IDbContextProvider<ITestFlexFieldsDbContext> dbContextProvider)
    {
        DbContextProvider = dbContextProvider;
    }

    public Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        TestArticle entity,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FlexFieldValue> fields = Definitions
            .Select(usage => new FlexFieldValue(
                usage.Field.ToFlexFieldData(),
                usage.Required,
                usage.Searchable,
                entity.GetField(usage.Field.Name)))
            .ToList();

        return Task.FromResult(fields);
    }

    public async Task<IReadOnlyList<TestArticle>> GetPagedEntitiesAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        return await dbContext.Articles
            .OrderBy(a => a.Id)   // stable ordering, as the contract requires
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public TestArticleFieldUsage AddDefinition(
        string name,
        string fieldTypeName,
        bool searchable = true,
        bool required = false)
    {
        var usage = new TestArticleFieldUsage(
            new TestField(Guid.NewGuid(), name, fieldTypeName), searchable, required);
        Definitions.Add(usage);
        return usage;
    }
}
