```json
//[doc-seo]
{
    "Description": "Rename or delete a FlexFields field definition safely: why the name is the key of the stored values, and how IFlexFieldValueMigrator moves or removes them."
}
```

# Renaming and Deleting Fields

A field definition's `Name` **is the key** its values are stored under, in the value bag of every entity. Renaming a field therefore leaves every entity holding its value under the old key, where nothing reads it, and deleting a field leaves its values in every bag forever. Both are data migrations, not edits, and `IFlexFieldValueMigrator<TEntity>` performs them.

The migrator has one implementation for every database provider, registered for every entity type, so you write no code for it beyond injecting it. FlexFields never calls it by itself: the field definition is your entity, so only your code knows when it is renamed or deleted.

## Renaming a Field

Follow this order, in one unit of work:

1. Rule the new name out as a duplicate, with `IFlexFieldRepository<TField>.NameExistsAsync(newName, excludedId: field.Id)`.
2. Change the `Name` of your field definition.
3. Call `RenameFieldAsync(oldName, newName)` **for each entity type** the field is attached to.

````csharp
public async Task RenameAsync(Guid id, string newName)
{
    if (await _fieldRepository.NameExistsAsync(newName, id))
    {
        throw new BusinessException("MyProject:DuplicateFieldName");
    }

    var field = await _fieldRepository.GetAsync(id);
    var oldName = field.Name;

    field.Name = newName;
    await _fieldRepository.UpdateAsync(field);

    await _productValueMigrator.RenameFieldAsync(oldName, newName);
}
````

`RenameFieldAsync` returns how many entities changed. It leaves alone the entities without the old key, so it is safe to run again.

The query index needs **no rebuild** after a rename: the index rows are keyed by the field id and the value, and a rename changes neither. A rebuild would in fact do harm: it reads the bags by name, and it must not run, nor any `SynchronizeAsync`, between steps 2 and 3, when the new name has no values yet.

## Deleting a Field

Remove the values first, then the definition:

````csharp
await _productValueMigrator.RemoveFieldAsync(field.Name);
await _fieldRepository.DeleteAsync(field);
````

On EF Core, the field's index rows are not queried any more once the definition is gone; delete them by their `FieldId` to keep the index table small.

## Field Definitions in Another Module

When the field definitions and the entities that carry values live in different modules, the module that renames a field cannot call the migrator of every entity type. `FlexFieldRenamedEto` (`OldName`, `NewName`) and `FlexFieldDeletedEto` exist for that case: the field module publishes them, and each module with entities handles them and calls its own migrator. FlexFields neither publishes nor handles them.

Calling the migrator directly is the safer default whenever you know the entity types:

* **Timing**: A distributed event published with ABP's default `onUnitOfWorkComplete: true` is handled after the rename has committed, and an entity saved in between loses its index rows for the field. Publish with `onUnitOfWorkComplete: false` to migrate in the same unit of work, or migrate in three idempotent steps (copy the value to the new key, change the name, drop the old key), so some key always matches the current name.
* **Errors**: An exception of a handler is not returned to the publisher. Check for a duplicate name before publishing.

## See Also

* [Querying by Field Values](querying.md)
* [Getting Started](getting-started.md)
