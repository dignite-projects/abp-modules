using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Demo.Permissions;
using Dignite.Abp.FlexFields.Demo.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace Dignite.Abp.FlexFields.Demo.Services;

/// <summary>
/// CRUD over <see cref="Product"/> plus filtering by flex field value - the demonstration this whole demo
/// exists for. Every write method follows the sequence the kernel's XML docs describe:
/// <c>ValidateAsync</c> before saving, <c>SynchronizeAsync</c> after, in the same unit of work.
/// </summary>
/// <remarks>
/// This host's <c>DemoModule</c> calls <c>AddAlwaysDisableUnitOfWorkTransaction()</c> - a SQLite-compatibility
/// choice the ABP Studio app-nolayers scaffold makes, not something this feature opted into. That means
/// "save, then synchronize" below is <b>not</b> wrapped in a database transaction here: a failure between the
/// two steps can leave the product saved with its index not yet caught up. In a host with transactions
/// enabled (the common case), the same two calls, in the same order, commit atomically for free - nothing
/// about how <c>SynchronizeAsync</c> is called changes. This isn't worth working around in a demo; it's worth
/// knowing about before copying this pattern into a host that made a different choice.
/// </remarks>
[Authorize]
public class ProductAppService : DemoAppService
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IFlexFieldProvider<Product> _flexFieldProvider;
    private readonly IFlexFieldValidator<Product> _validator;
    private readonly IFlexFieldIndexManager<Product> _indexManager;
    private readonly IFlexFieldQueryExecutor<Product> _queryExecutor;
    private readonly IFieldTypeResolver _fieldTypeResolver;

    public ProductAppService(
        IRepository<Product, Guid> productRepository,
        IFlexFieldProvider<Product> flexFieldProvider,
        IFlexFieldValidator<Product> validator,
        IFlexFieldIndexManager<Product> indexManager,
        IFlexFieldQueryExecutor<Product> queryExecutor,
        IFieldTypeResolver fieldTypeResolver)
    {
        _productRepository = productRepository;
        _flexFieldProvider = flexFieldProvider;
        _validator = validator;
        _indexManager = indexManager;
        _queryExecutor = queryExecutor;
        _fieldTypeResolver = fieldTypeResolver;
    }

    public virtual async Task<ProductDto> GetAsync(Guid id)
    {
        var product = await _productRepository.GetAsync(id);
        return await MapToDtoAsync(product);
    }

    /// <summary>
    /// Named <c>SearchAsync</c>, not <c>GetListAsync</c>, and POST rather than the GET a "Get*" name would
    /// default to: <see cref="GetProductListDto.FlexFieldConditions"/> is a list of objects, and ASP.NET
    /// Core's query-string binder plus Angular's <c>HttpParams.fromObject</c> (which only accepts primitives
    /// and arrays of primitives) cannot round-trip that shape through a query string - a filtered list is
    /// naturally POST-shaped once the filter itself is structured data.
    /// <para>
    /// The rename matters beyond taste: ABP's conventional controllers derive the URL from the METHOD NAME
    /// convention, not from an attribute's route template - <c>[HttpPost("list")]</c> on a method still named
    /// <c>GetListAsync</c> only overrides the verb, so the URL stays the convention's base resource path
    /// (<c>/api/app/product</c>), colliding with <see cref="CreateAsync"/>. A name outside the
    /// Get/GetList/Create/Update/Delete convention gets its own URL segment for free -
    /// <c>SearchAsync</c> becomes <c>POST /api/app/product/search</c>.
    /// </para>
    /// </summary>
    [HttpPost]
    public virtual async Task<PagedResultDto<ProductDto>> SearchAsync(GetProductListDto input)
    {
        var queryable = await _productRepository.GetQueryableAsync();

        if (input.FlexFieldConditions.Count > 0)
        {
            var conditions = input.FlexFieldConditions
                .Select(c => new FlexFieldQueryCondition(c.FieldId, c.FieldName, c.Operator, c.Value, c.ValueType))
                .ToList();

            // ApplyFilterAsync composes onto the queryable we pass it - it must come from the same
            // DbContext instance backing ProductFlexFieldQueryExecutor's index table, which it does here
            // because both resolve through IDemoDbContext.
            queryable = await _queryExecutor.ApplyFilterAsync(queryable, conditions);
        }

        var totalCount = await queryable.CountAsync();
        var products = await queryable
            .OrderBy(p => p.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        // One GetFlexFieldsAsync call per row - each re-reads the field-definition table. Fine at demo
        // scale; a host with a larger catalogue would cache the definitions for the page instead of
        // re-querying them per row.
        var dtos = new List<ProductDto>();
        foreach (var product in products)
        {
            dtos.Add(await MapToDtoAsync(product));
        }

        return new PagedResultDto<ProductDto>(totalCount, dtos);
    }

    [Authorize(DemoPermissions.Products.Create)]
    public virtual async Task<ProductDto> CreateAsync(CreateUpdateProductDto input)
    {
        var product = new Product(GuidGenerator.Create(), input.Name);
        await ApplyFlexFieldsAsync(product, input.FlexFields);

        await ValidateFlexFieldsAsync(product);

        await _productRepository.InsertAsync(product);

        // Same unit of work as the insert above - see the type-level remarks on why that matters here.
        await _indexManager.SynchronizeAsync(product);

        return await MapToDtoAsync(product);
    }

    [Authorize(DemoPermissions.Products.Update)]
    public virtual async Task<ProductDto> UpdateAsync(Guid id, CreateUpdateProductDto input)
    {
        var product = await _productRepository.GetAsync(id);
        product.Name = input.Name;
        await ApplyFlexFieldsAsync(product, input.FlexFields);

        await ValidateFlexFieldsAsync(product);

        await _productRepository.UpdateAsync(product);
        await _indexManager.SynchronizeAsync(product);

        return await MapToDtoAsync(product);
    }

    [Authorize(DemoPermissions.Products.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // No IFlexFieldIndexManager call needed on delete: ProductFlexFieldIndexes.ProductId carries
        // ON DELETE CASCADE (DemoDbContext.OnModelCreating), so the database drops the index rows itself.
        await _productRepository.DeleteAsync(id);
    }

    /// <summary>
    /// Writes each submitted value through <c>SetField</c> rather than replacing
    /// <see cref="Product.FlexFields"/> wholesale, so a key mapped to <c>null</c> removes that field
    /// instead of storing a null - the same contract <c>FlexFieldDictionaryExtensions.SetField</c>
    /// documents - and then canonicalizes what landed in the bag.
    /// </summary>
    private async Task ApplyFlexFieldsAsync(Product product, Dictionary<string, object?> values)
    {
        foreach (var (name, value) in values)
        {
            product.SetField(name, value);
        }

        await NormalizeFlexFieldsAsync(product);
    }

    /// <summary>
    /// Rewrites every value whose field type opts into <see cref="INormalizesValue"/> - today the two
    /// composite built-ins, Matrix and Table - into that type's canonical wire shape, before validation
    /// and before the bag is persisted.
    /// <para>
    /// Why a host has to do this at all: <see cref="IFieldType.Validate"/> parses a composite value
    /// case-insensitively but returns only errors, never the parsed value, so a structurally valid value
    /// with the wrong key casing saves without complaint and is then stored exactly as received -
    /// unreadable by every camelCase reader downstream, the Angular controls included. See
    /// <see cref="INormalizesValue"/> for the full reasoning, and for why this is deliberately not folded
    /// into <c>Validate</c>.
    /// </para>
    /// <para>
    /// Resolving the field type can throw for a name no longer registered, but that is not a new failure
    /// mode here: <see cref="ValidateFlexFieldsAsync"/> runs immediately after and resolves the same names
    /// through <c>FlexFieldValidator</c> unconditionally already.
    /// </para>
    /// </summary>
    private async Task NormalizeFlexFieldsAsync(Product product)
    {
        foreach (var flexField in await _flexFieldProvider.GetFlexFieldsAsync(product))
        {
            if (_fieldTypeResolver.Get(flexField.FieldTypeName) is INormalizesValue normalizer)
            {
                product.SetField(flexField.Name, normalizer.Normalize(flexField.Value));
            }
        }
    }

    private async Task ValidateFlexFieldsAsync(Product product)
    {
        var errors = await _validator.ValidateAsync(product);
        if (errors.Count > 0)
        {
            throw new AbpValidationException(errors.ToList());
        }
    }

    private async Task<ProductDto> MapToDtoAsync(Product product)
    {
        var dto = ObjectMapper.Map<Product, ProductDto>(product);

        var flexFieldValues = await _flexFieldProvider.GetFlexFieldsAsync(product);
        dto.FlexFieldValues = flexFieldValues.Select(v => v.ToDto()).ToList();

        return dto;
    }
}
