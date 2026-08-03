using System.Collections.Generic;
using Dignite.Abp.FlexFields.Demo.Entities;

namespace Dignite.Abp.FlexFields.Demo.Controllers;

public class ProductsWebViewModel
{
    public List<ProductRowViewModel> Products { get; }

    /// <summary>Field definitions only (Value is always null - see how the controller builds this) -
    /// enough to render the search form independently of any one product.</summary>
    public IReadOnlyList<FlexFieldValue> SearchFields { get; }

    public ProductsWebViewModel(List<ProductRowViewModel> products, IReadOnlyList<FlexFieldValue> searchFields)
    {
        Products = products;
        SearchFields = searchFields;
    }
}

public class ProductRowViewModel
{
    public Product Product { get; }

    public IReadOnlyList<FlexFieldValue> Fields { get; }

    public ProductRowViewModel(Product product, IReadOnlyList<FlexFieldValue> fields)
    {
        Product = product;
        Fields = fields;
    }
}
