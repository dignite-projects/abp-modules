```json
//[doc-seo]
{
    "Description": "Add runtime-defined fields to an entity of an ABP application with FlexFields and EF Core: the entities, the database mapping, the field provider, saving, validating and querying values."
}
```

# Getting Started

This guide adds runtime-defined fields to a `Product` entity of an ABP application on Entity Framework Core: an administrator defines product fields, every product carries values for them, and products can be filtered by those values. The module's [demo application](https://github.com/dignite-projects/abp-modules/tree/main/flex-fields/demo) is the same example, runnable, with an Angular and an MVC UI.

For MongoDB, read this guide for the concepts and then [MongoDB](mongodb.md) for the differences.

## Installing the Packages

| Layer | Package | Module |
|---|---|---|
| `Domain` | `Dignite.Abp.FlexFields.Domain` | `AbpFlexFieldsDomainModule` |
| `EntityFrameworkCore` | `Dignite.Abp.FlexFields.EntityFrameworkCore` | `AbpFlexFieldsEntityFrameworkCoreModule` |
| `Application.Contracts` | `Dignite.Abp.FlexFields.Abstractions`, for `FlexibleEntityDto` and the field types | `AbpFlexFieldsAbstractionsModule` |

Install each package into its layer and add the module to the `[DependsOn]` attribute of that layer's module class. `Dignite.Abp.FlexFields.Domain` brings `Abstractions` and `Domain.Shared` with it.

## Defining the Entities

You need three entities: the **field definition**, the **host entity** that carries values, and an **index row** that makes the values searchable.

````csharp
// The field definition
public class ProductField : AggregateRoot<Guid>, IFlexField
{
    public virtual string Name { get; set; } = string.Empty;
    public virtual string DisplayName { get; set; } = string.Empty;
    public virtual string? Description { get; set; }
    public virtual string FieldTypeName { get; set; } = string.Empty;
    public virtual FieldConfigurationDictionary Configuration { get; set; } = new();

    // The usage flags of this application. Here a field is used by products only,
    // so they live on the definition.
    public virtual bool Required { get; set; }
    public virtual bool Searchable { get; set; }
}

// The host entity
public class Product : AggregateRoot<Guid>, IHasFlexFields
{
    public virtual string Name { get; set; } = string.Empty;
    public virtual FlexFieldDictionary FlexFields { get; set; } = new();

    protected Product()
    {
    }

    public Product(Guid id, string name)
        : base(id)
    {
        Name = name;
    }
}

// One searchable value of one product
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
````

> `Required` and `Searchable` are not part of `IFlexField`. When one field definition is attached to several entity types, for example a CMS's fields shared by several content types, keep them on a separate usage entity instead, one row per field and entity type.

## Mapping the Entities

Map the three entities in your `DbContext`. The `Configure…` extension methods map the members FlexFields knows about; everything else, including the table names, keys and relations, stays yours:

````csharp
builder.Entity<Product>(b =>
{
    b.ToTable(MyProjectConsts.DbTablePrefix + "Products");
    b.ConfigureByConvention();
    b.ConfigureFlexFieldsProperty(); // the value bag, as a JSON column
});

builder.Entity<ProductField>(b =>
{
    b.ToTable(MyProjectConsts.DbTablePrefix + "ProductFields");
    b.ConfigureByConvention();
    b.ConfigureFlexField(); // Name, DisplayName, Description, FieldTypeName, Configuration
    b.HasIndex(x => x.Name).IsUnique();
});

builder.Entity<ProductFlexFieldIndex>(b =>
{
    b.ToTable(MyProjectConsts.DbTablePrefix + "ProductFlexFieldIndexes");
    b.ConfigureByConvention();
    b.ConfigureFlexFieldIndex(); // FieldId, ValueType and one typed column per value type
    b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    b.HasIndex(x => new { x.ProductId, x.FieldId });
});
````

* FlexFields adds no unique index on the field name; the `Name` must be unique, so add one.
* FlexFields maps no relation between the index rows and the host entity. With the cascade delete above, deleting a product deletes its index rows, and nothing else needs to happen on delete.

Then add a migration, as for any other entity.

## Implementing the Field Provider

`IFlexFieldProvider<TEntity>` is the one seam you implement. It merges your field definitions, your usage flags and the values in the entity's bag into `FlexFieldValue` objects, and it pages through all entities when the index is rebuilt:

````csharp
public class ProductFlexFieldProvider : IFlexFieldProvider<Product>, ITransientDependency
{
    private readonly IRepository<ProductField, Guid> _fieldRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ProductFlexFieldProvider(
        IRepository<ProductField, Guid> fieldRepository,
        IRepository<Product, Guid> productRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _fieldRepository = fieldRepository;
        _productRepository = productRepository;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        Product entity,
        CancellationToken cancellationToken = default)
    {
        var fields = await _fieldRepository.GetListAsync(cancellationToken: cancellationToken);

        return fields
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
        var queryable = await _productRepository.GetQueryableAsync();

        // The order must be stable, or a rebuild can skip entities across pages
        return await _asyncExecuter.ToListAsync(
            queryable.OrderBy(p => p.Id).Skip(skipCount).Take(maxResultCount),
            cancellationToken);
    }
}
````

`ToFlexFieldData()` converts your definition entity to the plain `FlexFieldData` the kernel works with. A provider that reads all definitions for every entity is fine at this scale; with a large catalog, cache the definitions.

## Adopting the Index Manager, the Query Executor and the Repository

The EF Core package has base classes for the other three; your classes only name the foreign key of the index row and create it:

````csharp
public class ProductFlexFieldIndexManager
    : EfCoreFlexFieldIndexManagerBase<IMyProjectDbContext, Product, ProductFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(ProductFlexFieldIndex.ProductId);

    public ProductFlexFieldIndexManager(
        IDbContextProvider<IMyProjectDbContext> dbContextProvider,
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

public class ProductFlexFieldQueryExecutor
    : EfCoreFlexFieldQueryExecutorBase<IMyProjectDbContext, Product, ProductFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(ProductFlexFieldIndex.ProductId);

    public ProductFlexFieldQueryExecutor(IDbContextProvider<IMyProjectDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}

public class ProductFieldRepository : EfCoreFlexFieldRepositoryBase<IMyProjectDbContext, ProductField>
{
    public ProductFieldRepository(IDbContextProvider<IMyProjectDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
````

Register the repository in the `ConfigureServices` method of your EF Core module. ABP exposes a custom repository interface automatically only when the class name ends with the interface name, which `ProductFieldRepository` does not, so `IFlexFieldRepository<ProductField>` needs one explicit line:

````csharp
context.Services.AddAbpDbContext<MyProjectDbContext>(options =>
{
    options.AddDefaultRepositories(includeAllEntities: true);
    options.AddRepository<ProductField, ProductFieldRepository>();
});

context.Services.AddTransient<IFlexFieldRepository<ProductField>, ProductFieldRepository>();
````

`IFlexFieldValidator<Product>` and `IFlexFieldValueMigrator<Product>` need no code: their default implementations are registered for every entity type.

## Saving Values

In the application service that saves a product, write the submitted values into the bag, validate them, save the product, and then **synchronize the index in the same unit of work**:

````csharp
public async Task<ProductDto> CreateAsync(CreateUpdateProductDto input)
{
    var product = new Product(GuidGenerator.Create(), input.Name);

    foreach (var (name, value) in input.FlexFields)
    {
        product.SetField(name, value); // a null value removes the field
    }

    var errors = await _flexFieldValidator.ValidateAsync(product);
    if (errors.Count > 0)
    {
        throw new AbpValidationException(errors.ToList());
    }

    await _productRepository.InsertAsync(product);
    await _flexFieldIndexManager.SynchronizeAsync(product);

    return ObjectMapper.Map<Product, ProductDto>(product);
}
````

* `SetField`, `GetField`, `RemoveField` and `HasField` are extension methods of `IHasFlexFields`. `GetField<T>` also unwraps the shapes a value comes back in after a round trip through the database, such as a `JsonElement`.
* The validator returns the errors instead of throwing, so you decide how to report them; here they become ABP's validation error response. The messages are localized from the `FlexFields` resource.
* For the composite [Matrix and Table](field-types.md#composite-field-types) types, normalize the values before validating them; see [Normalizing Values](field-types.md#normalizing-values).
* Synchronizing in the same unit of work commits the index rows atomically with the value bag they are derived from.

To return the values, derive the DTO from `FlexibleEntityDto`, which has the `FlexFields` property:

````csharp
public class ProductDto : FlexibleEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
````

## Querying by Field Values

Pass the host query and the conditions to the query executor. It adds the conditions to the query, in the database, and leaves the rest of the query to you:

````csharp
var queryable = await _productRepository.GetQueryableAsync();

queryable = await _flexFieldQueryExecutor.ApplyFilterAsync(queryable,
[
    new FlexFieldQueryCondition(colorField.Id, colorField.Name,
        FlexFieldQueryOperator.Equals, "Red", FlexFieldValueType.String),
    new FlexFieldQueryCondition(weightField.Id, weightField.Name,
        FlexFieldQueryOperator.LessThanOrEqual, "2.5", FlexFieldValueType.Number)
]);

var products = await AsyncExecuter.ToListAsync(queryable.OrderBy(p => p.Name).PageBy(input));
````

Only the values of **searchable** fields are indexed. See [Querying by Field Values](querying.md) for the operators, and for when the index has to be rebuilt.

## Managing the Field Definitions

The field definitions are your entities, managed by your own application service. Three things need FlexFields:

* Offer the field types to choose from with `IFieldTypeResolver.GetAll()`.
* After switching a field's `Searchable` flag on, or changing its field type, rebuild the index with `IFlexFieldIndexManager<Product>.RebuildAsync()`.
* A field's `Name` is the key of its values, so renaming or deleting a field is a data migration; see [Renaming and Deleting Fields](renaming-and-deleting-fields.md).

## The UI

* **Angular**: `@dignite/ng.flex-fields` has components to configure a field, edit, display and filter its value. See [Angular UI](angular-ui.md).
* **MVC**: `Dignite.Abp.FlexFields.Web` has tag helpers to display a value and render a search input. See [MVC UI](mvc-ui.md).

## See Also

* [Field Types](field-types.md)
* [Querying by Field Values](querying.md)
* [Renaming and Deleting Fields](renaming-and-deleting-fields.md)
