using System;
using Dignite.Abp.FlexFields.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Entities;

/// <summary>
/// The typed, per-value projection of <see cref="Product.FlexFields"/> that lets "products where field X
/// matches Y" run as SQL. The kernel maps no relationship to the host - <see cref="ProductId"/> and its
/// foreign key are this demo's own, added in <c>DemoDbContext.OnModelCreating</c>.
/// </summary>
public class ProductFlexFieldIndex : FlexFieldIndexBase<Product>
{
    public virtual Guid ProductId { get; set; }

    protected ProductFlexFieldIndex()
    {
    }

    public ProductFlexFieldIndex(Guid id, Guid productId, Guid fieldId, FlexFieldIndexValue value)
        : base(id)
    {
        ProductId = productId;
        SetValue(fieldId, value);
    }
}
