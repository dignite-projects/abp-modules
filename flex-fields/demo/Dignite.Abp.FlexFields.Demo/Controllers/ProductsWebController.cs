using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Demo.Permissions;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.FlexFields.Demo.Controllers;

/// <summary>
/// Server-rendered counterpart to the Angular admin's products page: proves
/// Dignite.Abp.FlexFields.Web's &lt;flex-field-view&gt;/&lt;flex-field-search&gt; against this demo's
/// real Product/ProductField data, and doubles as a manual smoke test alongside
/// Dignite.Abp.FlexFields.Web.Tests. Read-only - create/edit/delete stay the Angular admin's job.
/// </summary>
[Authorize(DemoPermissions.Products.Default)]
public class ProductsWebController : AbpController
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IFlexFieldProvider<Product> _flexFieldProvider;
    private readonly IFlexFieldQueryExecutor<Product> _queryExecutor;

    public ProductsWebController(
        IRepository<Product, Guid> productRepository,
        IFlexFieldProvider<Product> flexFieldProvider,
        IFlexFieldQueryExecutor<Product> queryExecutor)
    {
        _productRepository = productRepository;
        _flexFieldProvider = flexFieldProvider;
        _queryExecutor = queryExecutor;
    }

    public async Task<IActionResult> Index()
    {
        // Field *definitions* don't depend on any one product - a transient, never-persisted Product
        // has an empty value bag, so GetFlexFieldsAsync on it returns every definition with Value ==
        // null. Used only to render the search form; never saved.
        var searchFields = await _flexFieldProvider.GetFlexFieldsAsync(new Product(Guid.Empty, string.Empty));

        var queryable = await _productRepository.GetQueryableAsync();
        var conditions = BuildConditions(searchFields);
        if (conditions.Count > 0)
        {
            queryable = await _queryExecutor.ApplyFilterAsync(queryable, conditions);
        }

        var products = queryable.OrderBy(p => p.Name).ToList();

        var rows = new List<ProductRowViewModel>();
        foreach (var product in products)
        {
            rows.Add(new ProductRowViewModel(product, await _flexFieldProvider.GetFlexFieldsAsync(product)));
        }

        return View(new ProductsWebViewModel(rows, searchFields));
    }

    /// <summary>
    /// The translation &lt;flex-field-search&gt; deliberately doesn't do - reads back exactly the input
    /// names its own default search partials render (see
    /// Dignite.Abp.FlexFields.Web/Views/Shared/FlexFields/Search/*.cshtml: "{Name}", "{Name}Min"/"Max",
    /// "{Name}From"/"To") and turns whatever was submitted into <see cref="FlexFieldQueryCondition"/>s.
    /// </summary>
    private List<FlexFieldQueryCondition> BuildConditions(IReadOnlyList<FlexFieldValue> fields)
    {
        var conditions = new List<FlexFieldQueryCondition>();

        foreach (var field in fields.Where(f => f.Searchable))
        {
            switch (field.FieldTypeName)
            {
                case TextFieldType.ControlName:
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.Contains, FlexFieldValueType.String, Request.Query[field.Name]);
                    break;

                case NumberFieldType.ControlName:
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.GreaterThanOrEqual, FlexFieldValueType.Number, Request.Query[field.Name + "Min"]);
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.LessThanOrEqual, FlexFieldValueType.Number, Request.Query[field.Name + "Max"]);
                    break;

                case DateTimeFieldType.ControlName:
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.GreaterThanOrEqual, FlexFieldValueType.DateTime, Request.Query[field.Name + "From"]);
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.LessThanOrEqual, FlexFieldValueType.DateTime, Request.Query[field.Name + "To"]);
                    break;

                case SelectFieldType.ControlName:
                case TreeFieldType.ControlName:
                    AddInIfPresent(conditions, field, FlexFieldValueType.String, Request.Query[field.Name]);
                    break;

                case BooleanFieldType.ControlName:
                    AddIfPresent(conditions, field, FlexFieldQueryOperator.Equals, FlexFieldValueType.Boolean, Request.Query[field.Name]);
                    break;
            }
        }

        return conditions;
    }

    private static void AddIfPresent(
        List<FlexFieldQueryCondition> conditions,
        FlexFieldValue field,
        FlexFieldQueryOperator @operator,
        FlexFieldValueType valueType,
        StringValues raw)
    {
        var value = raw.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        conditions.Add(new FlexFieldQueryCondition(field.FieldId, field.Name, @operator, value, valueType));
    }

    private static void AddInIfPresent(
        List<FlexFieldQueryCondition> conditions,
        FlexFieldValue field,
        FlexFieldValueType valueType,
        StringValues raw)
    {
        var values = raw.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (values.Count == 0)
        {
            return;
        }

        conditions.Add(new FlexFieldQueryCondition(field.FieldId, field.Name, FlexFieldQueryOperator.In, string.Join(",", values), valueType));
    }
}
