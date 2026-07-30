using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Demo.Data;
using Dignite.Abp.FlexFields.Demo.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Services.FlexFields;

/// <summary>
/// The kernel's only information entry point for <see cref="Product"/>: merges each field's definition
/// (from <see cref="IDemoDbContext.ProductFields"/>), its Required/Searchable flags, and its value (from
/// the product's own bag) into a <see cref="FlexFieldValue"/>.
/// </summary>
/// <remarks>
/// Unlike the kernel's own worked example
/// (<c>flex-fields/test/Dignite.Abp.FlexFields.EntityFrameworkCore.Tests/TestArticleFlexFieldProvider.cs</c>),
/// which reads definitions from an in-memory list to keep tests simple, this reads them from the real
/// <c>AppProductFields</c> table - the shape a production downstream would actually use.
/// </remarks>
public class ProductFlexFieldProvider : IFlexFieldProvider<Product>, ITransientDependency
{
    protected IDbContextProvider<IDemoDbContext> DbContextProvider { get; }

    public ProductFlexFieldProvider(IDbContextProvider<IDemoDbContext> dbContextProvider)
    {
        DbContextProvider = dbContextProvider;
    }

    public async Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        Product entity,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        var definitions = await dbContext.ProductFields.ToListAsync(cancellationToken);

        return definitions
            .Select(field => new FlexFieldValue(
                field.ToFlexFieldData(),
                field.Required,
                field.Searchable,
                entity.GetField(field.Name)))
            .ToList();
    }

    public async Task<IReadOnlyList<Product>> GetPagedEntitiesAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        // Stable ordering is part of the contract: IFlexFieldIndexManager.RebuildAsync pages through
        // every entity, and unstable ordering can silently skip rows across page boundaries.
        return await dbContext.Products
            .OrderBy(p => p.Id)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }
}
