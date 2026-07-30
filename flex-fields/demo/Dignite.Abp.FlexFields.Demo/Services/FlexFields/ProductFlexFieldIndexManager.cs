using System;
using Dignite.Abp.FlexFields.Demo.Data;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Services.FlexFields;

/// <summary>
/// Projects each <see cref="Product"/>'s searchable field values into <see cref="IDemoDbContext.ProductFlexFieldIndexes"/>
/// so "products where field X matches Y" can run as SQL. <c>ITransientDependency</c> is what exposes this
/// as <c>IFlexFieldIndexManager&lt;Product&gt;</c> - the concrete class name ends with
/// <c>FlexFieldIndexManager</c>, matching ABP's conventional-registration rule.
/// </summary>
public class ProductFlexFieldIndexManager
    : EfCoreFlexFieldIndexManagerBase<IDemoDbContext, Product, ProductFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(ProductFlexFieldIndex.ProductId);

    public ProductFlexFieldIndexManager(
        IDbContextProvider<IDemoDbContext> dbContextProvider,
        IFlexFieldProvider<Product> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver)
        : base(dbContextProvider, flexFieldProvider, fieldTypeResolver)
    {
    }

    protected override ProductFlexFieldIndex CreateIndexRow(Guid entityId, Guid fieldId, FlexFieldIndexValue value)
    {
        return new ProductFlexFieldIndex(Guid.NewGuid(), entityId, fieldId, value);
    }
}
