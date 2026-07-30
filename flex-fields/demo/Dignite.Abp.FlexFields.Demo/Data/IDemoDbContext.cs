using Dignite.Abp.FlexFields.Demo.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Data;

/// <summary>
/// This demo's own DbContext interface - the FlexFields kernel ships none of its own, since it owns no
/// tables. This is the <c>TDbContext</c> the kernel's EF Core base classes (index manager, query
/// executor, field repository) are parameterized on.
/// </summary>
public interface IDemoDbContext : IEfCoreDbContext
{
    DbSet<Product> Products { get; }

    DbSet<ProductField> ProductFields { get; }

    DbSet<ProductFlexFieldIndex> ProductFlexFieldIndexes { get; }
}
