```json
//[doc-seo]
{
    "Description": "Use FlexFields with MongoDB: the value bag embedded in the document, one wildcard index over it, and the base classes for the index manager, query executor and field repository."
}
```

# MongoDB

The `Dignite.Abp.FlexFields.MongoDB` package stores and queries the value bag **in place**, inside the host entity's document. There is no index table: the bag is both the authoritative storage and the thing that is indexed, with one wildcard index. That is the reason FlexFields has two separate providers: the EF Core provider's typed index row (`FlexFieldIndexValue`) has no MongoDB counterpart.

Read [Getting Started](getting-started.md) first; this page covers what differs.

## Installation

Install the `Dignite.Abp.FlexFields.MongoDB` package into your MongoDB project and depend on `AbpFlexFieldsMongoDbModule`. It registers the BSON serializers of the value bag.

## Mapping the Collection

Your host entity needs no index entity. Add the wildcard index over its value bag to its collection, in the `CreateModel` method of your MongoDB context:

````csharp
modelBuilder.Entity<Product>(b =>
{
    b.CollectionName = MyProjectConsts.DbTablePrefix + "Products";
    b.ConfigureIndexes(indexes => indexes.ConfigureFlexFieldsIndex());
});
````

The value bag is stored in the `FlexFields` element of the document. One wildcard index covers every key in it, present and future, including keys a rename introduces, so nothing has to be created or dropped as fields are added and made searchable. The trade-off is that a wildcard index serves one path per query and cannot be compounded with your own fields; add a compound index of your own next to it if you need one.

## Adopting the Base Classes

````csharp
public class ProductFlexFieldIndexManager
    : MongoFlexFieldIndexManagerBase<IMyProjectMongoDbContext, Product>,
      ITransientDependency
{
    public ProductFlexFieldIndexManager(
        IMongoDbContextProvider<IMyProjectMongoDbContext> dbContextProvider,
        IFlexFieldProvider<Product> flexFieldProvider,
        IBasicRepository<Product> repository,
        IFieldTypeResolver fieldTypeResolver,
        IUnitOfWorkManager unitOfWorkManager)
        : base(dbContextProvider, flexFieldProvider, repository, fieldTypeResolver, unitOfWorkManager)
    {
    }
}

public class ProductFlexFieldQueryExecutor
    : MongoFlexFieldQueryExecutorBase<IMyProjectMongoDbContext, Product>,
      ITransientDependency
{
    public ProductFlexFieldQueryExecutor(IMongoDbContextProvider<IMyProjectMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}

public class ProductFieldRepository : MongoFlexFieldRepositoryBase<IMyProjectMongoDbContext, ProductField>
{
    public ProductFieldRepository(IMongoDbContextProvider<IMyProjectMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
````

As with EF Core, register `IFlexFieldRepository<ProductField>` for the repository explicitly.

## How Indexing Works

With no index table, the index manager meets the same guarantee in the bag itself: it writes each searchable value back into the bag **in the typed form** of its field type's `IndexValueType`, so a native filter has a well-typed value to compare against. Without it, a number stored as the string `"42.5"` would never match a numeric range. A value already in its typed form is left alone and costs no write, so an application that writes well-typed values pays nothing for synchronizing.

Which values are eligible is the same rule as on EF Core: only searchable usages of an indexable field type. So switching a field's `Searchable` flag on needs a `RebuildAsync` here too, and `RebuildAsync` also ensures the wildcard index exists.

## How Querying Works

The query executor addresses a value by the field's **current name** in the bag, which is why a condition carries both the field id and the name. It finds the matching entity ids with a native filter and adds them to your query. Override the members of `MongoFlexFieldQueryExecutorBase` to change the details:

* `MaxMatchedIdCount` (10,000 by default): The most matching ids a filter may carry into your query. A filter that matches more throws instead of truncating the result; narrow the conditions, or raise the limit.
* `ContainsIsCaseInsensitive` (`false` by default): Whether `Contains` ignores case.

## See Also

* [Querying by Field Values](querying.md)
* [MongoDB Integration](https://abp.io/docs/latest/framework/data/mongodb)
