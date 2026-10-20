```json
//[doc-seo]
{
    "Description": "Add runtime-defined fields to any entity of an ABP application with FlexFields: field types, configuration, validation, a value bag and a query index, on EF Core or MongoDB."
}
```

# FlexFields Module

FlexFields adds **runtime-defined fields** to the entities of an ABP application: an administrator defines the fields of a product, an article or a form while the application runs, and every entity of that type carries values for them. FlexFields supplies the field types, their configuration, validation, the storage of the values and a query index, with EF Core and MongoDB providers and Angular and MVC UI.

It is a **constraint kernel**, not a ready-made module, modeled on ABP's own `Volo.Abp.Users`: it supplies contracts, base classes and mechanism, and **owns no field or entity of its own**. Your application defines its own field-definition entity, its own entities that carry values, and its own database tables; FlexFields never defines "the" field table. That is what lets a CMS, a product catalog and a form builder each use it with their own rules.

## Concepts

* **Field definition**: An entity of yours that implements `IFlexField`: a unique `Name`, a `DisplayName`, the `FieldTypeName` of its field type and a `Configuration` dictionary. A CMS calls it a field; a shop might call it a product attribute.
* **Host entity**: An entity of yours that implements `IHasFlexFields`. Its `FlexFields` property is a `FlexFieldDictionary`, the **value bag** that holds its values, keyed by the field names.
* **Field type**: The behavior of a field: how its configuration is read, how its value is validated, and which of its values can be searched. FlexFields has [eight built-in field types](field-types.md), and you can [add your own](custom-field-types.md).
* **Usage**: Whether a field is **required** or **searchable** belongs to the field's use on a host entity type, not to the definition, because one definition can be attached to several host types with different rules.
* **`FlexFieldValue`**: One field of one host entity at runtime: its definition, its usage flags and its current value. Your application produces these, and everything in FlexFields works on them.

## What You Implement, What You Get

You implement one seam, **`IFlexFieldProvider<TEntity>`**, which tells FlexFields what fields a host entity has and what their values are. FlexFields gives back:

* `IFlexFieldValidator<TEntity>`: Validates an entity's values with the rules of their field types.
* `IFlexFieldIndexManager<TEntity>`: Keeps a query index in step with the value bags.
* `IFlexFieldQueryExecutor<TEntity>`: Filters a host query by field values, in the database.
* `IFlexFieldValueMigrator<TEntity>`: Moves or removes values when a field is renamed or deleted.

The database providers give you base classes for the index manager, the query executor and a repository of your field definitions, so adopting them is mostly a constructor. [Getting Started](getting-started.md) wires all of it to an entity.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.FlexFields.Abstractions` | The field types, the value bag, `FlexFieldValue`, the query vocabulary and the field lifecycle events. Free of DDD; enough on its own to implement a field type or to type your DTOs. |
| `Dignite.Abp.FlexFields.Domain.Shared` | Shared constants (`FlexFieldConsts`). |
| `Dignite.Abp.FlexFields.Domain` | `IFlexField`, `IFlexFieldProvider<TEntity>` and the other seams, with the default validator and value migrator. |
| `Dignite.Abp.FlexFields.EntityFrameworkCore` | Model mapping extensions, the typed index row and base classes for the index manager, the query executor and the field repository. Ships no `DbContext` and no table. |
| `Dignite.Abp.FlexFields.MongoDB` | Queries and indexes the value bag in place. See [MongoDB](mongodb.md). |
| `Dignite.Abp.FlexFields.Web` | MVC tag helpers that render field values and search inputs. See [MVC UI](mvc-ui.md). |
| `Dignite.Abp.FlexFields.CKEditor`, `.CKEditor.Web` | An optional rich-text field type. See [CKEditor Field Type](ckeditor.md). |
| `@dignite/ng.flex-fields` (npm) | Angular components to configure, edit, display and filter fields of every built-in type. See [Angular UI](angular-ui.md). |
| `@dignite/ng.flex-fields-ckeditor` (npm) | The Angular half of the CKEditor field type. |

FlexFields has no application layer and no HTTP API of its own: there is no field model to expose. Your application exposes its own field definitions and entities, with their value bags, through its own application services.

## The Value Bag

FlexFields stores values in a **value bag of its own**, not in ABP's `ExtraProperties`. `ExtraProperties` belongs to [object extensions](https://abp.io/docs/latest/framework/fundamentals/object-extensions) and is shared with every module that extends an entity; mixing runtime-defined fields into it would make the two indistinguishable. The value bag is the authoritative storage: the query index is derived from it and can always be rebuilt.

## See Also

* [Getting Started](getting-started.md)
* [Field Types](field-types.md)
