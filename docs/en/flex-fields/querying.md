```json
//[doc-seo]
{
    "Description": "Filter ABP entities by FlexFields values in the database: the query index, query conditions and operators, synchronizing and rebuilding the index."
}
```

# Querying by Field Values

FlexFields filters a host query by field values **in the database**, never in memory. Two services work together:

* `IFlexFieldIndexManager<TEntity>` keeps the **query index** in step with the value bags.
* `IFlexFieldQueryExecutor<TEntity>` adds **conditions** to a host query, against that index.

The value bag is the authoritative storage, and the index is derived from it: it can always be rebuilt from the bags.

## The Query Index

Only the values of **searchable** usages of an **indexable** field type are indexed. A field type says which typed slot its values go into with its `IndexValueType` (`String`, `Number`, `DateTime`, `Boolean` or `Guid`), and which values to index with `GetSearchableValues`; a multi-valued type such as `Select` indexes each selected value. Field types with no `IndexValueType`, such as `Matrix`, `Table` and `CKEditor`, are never indexed.

The providers keep the index differently:

* **Entity Framework Core**: An index table of yours, of `FlexFieldIndexBase<TEntity>` rows, one per searchable value, with one typed column per value type. See [Getting Started](getting-started.md#defining-the-entities).
* **MongoDB**: No table. The value bag itself is indexed, with a wildcard index, and the index manager writes each searchable value back into the bag in its typed form. See [MongoDB](mongodb.md).

### Synchronizing

Call `SynchronizeAsync` after saving an entity, **in the same unit of work**, so the index commits atomically with the value bag it was derived from:

````csharp
await _productRepository.UpdateAsync(product);
await _flexFieldIndexManager.SynchronizeAsync(product);
````

On EF Core, delete an entity's index rows with it, for example with a cascade delete on the index table's foreign key.

### Rebuilding

`RebuildAsync` re-derives the index of every entity, paging through them with `IFlexFieldProvider<TEntity>.GetPagedEntitiesAsync`. Rebuild after:

* switching a field's `Searchable` flag on, or off;
* changing a field's type, so its values belong in another typed slot.

Do **not** rebuild after renaming a field: a rebuild reads the bags by the field's name, so it would find nothing under the new name and delete the existing rows. See [Renaming and Deleting Fields](renaming-and-deleting-fields.md).

## Query Conditions

A `FlexFieldQueryCondition` is self-describing, so the query side needs no lookup of the field definition:

````csharp
new FlexFieldQueryCondition(
    fieldId: field.Id,
    fieldName: field.Name,
    @operator: FlexFieldQueryOperator.GreaterThanOrEqual,
    value: "100",
    valueType: FlexFieldValueType.Number);
````

* **FieldId** and **FieldName**: Both are required. The EF Core index rows are keyed by the field id; MongoDB addresses the value in the bag by the field's current name.
* **Operator**: See the table below.
* **Value** (`string`): The value to compare with, in invariant format. For `In`, a comma-separated list.
* **ValueType**: The typed slot to compare in, the field type's `IndexValueType`.

The operators each value type supports:

| Value type | Operators |
|---|---|
| `String` | `Equals`, `NotEquals`, `Contains`, `In` |
| `Number` | `Equals`, `NotEquals`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `In` |
| `DateTime` | `Equals`, `NotEquals`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `In` |
| `Boolean` | `Equals`, `NotEquals` |
| `Guid` | `Equals`, `NotEquals`, `In` |

An unsupported combination throws. `FlexFieldQueryConditions.IsOperatorSupported` tells you in advance, for a UI that offers operators.

## Applying the Conditions

`ApplyFilterAsync` returns the host query narrowed to the entities that match **all** conditions. It only adds predicates, so paging, sorting and your own filters compose on top of the result:

````csharp
var queryable = await _productRepository.GetQueryableAsync();
queryable = queryable.Where(p => p.IsPublished);
queryable = await _flexFieldQueryExecutor.ApplyFilterAsync(queryable, conditions);

var totalCount = await AsyncExecuter.CountAsync(queryable);
var products = await AsyncExecuter.ToListAsync(queryable.OrderBy(p => p.Name).PageBy(input));
````

* Two conditions on the same field are combined, so a `GreaterThanOrEqual` and a `LessThanOrEqual` make a range.
* An empty list of conditions is rejected: that is your own unfiltered query, not a field query. Call `ApplyFilterAsync` only when there are conditions.
* On EF Core, the host query and the index table must come from the same `DbContext`.

### Taking Conditions from a Client

`FlexFieldQueryCondition` has a parameterless constructor, so it can be part of an input DTO. A list of objects does not travel well in a query string, so use a POST method, such as a `SearchAsync` method of your application service, for a filtered list.

The search components of the [Angular](angular-ui.md#search) and [MVC](mvc-ui.md#search-inputs) UIs render the inputs of a filter form; turning what a user entered into conditions is your application's job, because it depends on the field type.

## See Also

* [Getting Started](getting-started.md)
* [MongoDB](mongodb.md)
