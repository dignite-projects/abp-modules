using Dignite.Abp.FlexFields.Demo.Data;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Services.FlexFields;

/// <summary>
/// Pushes flex-field filter conditions down into SQL against <see cref="IDemoDbContext.ProductFlexFieldIndexes"/>,
/// one correlated <c>WHERE Id IN (...)</c> subquery per condition. Adopting the kernel's base class costs
/// exactly one property override.
/// </summary>
public class ProductFlexFieldQueryExecutor
    : EfCoreFlexFieldQueryExecutorBase<IDemoDbContext, Product, ProductFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(ProductFlexFieldIndex.ProductId);

    public ProductFlexFieldQueryExecutor(IDbContextProvider<IDemoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
