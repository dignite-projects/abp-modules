```json
//[doc-seo]
{
    "Description": "The eight built-in FlexFields field types: Text, Number, DateTime, Select, Boolean, Tree, and the composite Matrix and Table, with their configuration keys and values."
}
```

# Field Types

A field type is the behavior a field is bound to through its `FieldTypeName`: how its configuration is read, how its value is validated, and which of its values go into the query index. FlexFields has eight built-in field types in the `Dignite.Abp.FlexFields.Abstractions` package, and you can [add your own](custom-field-types.md).

| Name | Value | Indexed as | Configuration class |
|---|---|---|---|
| `Text` | A string | `String` | `TextConfiguration` |
| `Number` | A number | `Number` | `NumberConfiguration` |
| `DateTime` | A date, a date and time, or a month | `DateTime` | `DateTimeConfiguration` |
| `Select` | One option value, or several | `String`, one per value | `SelectConfiguration` |
| `Boolean` | `true` or `false` | `Boolean` | `BooleanConfiguration` |
| `Tree` | One node value, or several | `String`, one per value | `TreeConfiguration` |
| `Matrix` | A list of blocks, each of a block type with its own fields | Not indexed | `MatrixConfiguration` |
| `Table` | A list of rows with the same columns | Not indexed | `TableConfiguration` |

> The names and the configuration keys below are **stored data**: they are in every field definition your application saved. Never rename them, and do not reuse them for a field type of your own.

## Configuration

A field's `Configuration` is a `FieldConfigurationDictionary` of keys and values. Each field type has a configuration class that reads and writes it with typed properties, so you rarely touch the keys:

````csharp
var configuration = new TextConfiguration(field.Configuration)
{
    Mode = TextMode.MultipleLine,
    CharLimit = 2000
};
````

`IFieldType.GetConfiguration(dictionary)` returns the configuration class of a field type, for code that works on any field type.

### Text

| Key | Property | Default | Description |
|---|---|---|---|
| `Text.Mode` | `Mode` | `SingleLine` | `SingleLine` or `MultipleLine`. |
| `Text.CharLimit` | `CharLimit` | 256 for a single line, 1024 for multiple lines | The longest value accepted. |
| `Text.Placeholder` | `Placeholder` | `null` | The placeholder of the input. |

### Number

| Key | Property | Default | Description |
|---|---|---|---|
| `Number.Min` | `Min` | `null` | The smallest value accepted. |
| `Number.Max` | `Max` | `null` | The largest value accepted. |
| `Number.Decimals` | `Decimals` | 2 | The number of decimal places. |
| `Number.Step` | `Step` | `null` | The interval between valid values, for the input. |
| `FormatSpecifier` | `FormatSpecifier` | `null` | A .NET numeric format string, for display. |

### DateTime

| Key | Property | Default | Description |
|---|---|---|---|
| `DateTime.InputMode` | `InputMode` | `Date` | `Date`, `DateTime` or `Month`. |
| `DateTime.Min` | `Min` | `null` | The earliest value accepted. |
| `DateTime.Max` | `Max` | `null` | The latest value accepted. |

### Select

| Key | Property | Default | Description |
|---|---|---|---|
| `Select.Options` | `Options` | Empty | The options, each a `SelectListItem` with a `Text`, a `Value` and a `Selected` flag. |
| `Select.Multiple` | `Multiple` | `false` | Whether several options can be selected. The value is then a list of option values. |
| `Select.NullText` | `NullText` | `null` | The text of the empty choice. |
| `Select.Size` | `Size` | `null` | The number of visible rows of the input. |

A value that is not one of the options' values fails validation.

### Boolean

| Key | Property | Default | Description |
|---|---|---|---|
| `Boolean.Default` | `Default` | `false` | The initial value of a new entity. |

### Tree

| Key | Property | Default | Description |
|---|---|---|---|
| `Tree.Nodes` | `Nodes` | Empty | The tree, of `TreeNodeItem`s with a `Text`, a `Value`, a `Selected` flag and `Children`. |
| `Tree.Multiple` | `Multiple` | `false` | Whether several nodes can be selected. The value is then a list of node values. |

A value that is not the value of a node of the tree fails validation.

## Values

A value bag holds plain values: strings, numbers, booleans, lists and, for the composite types, lists of objects. After a round trip through the database, a value may come back in another CLR shape than it was written in, such as a `JsonElement` from an EF Core JSON column or a `List<object>` from MongoDB. The built-in field types read every one of these shapes, and `GetField<T>(name)` converts them for your own code.

The messages of the validation errors come from the `FlexFields` [localization resource](https://abp.io/docs/latest/framework/fundamentals/localization), which `Dignite.Abp.FlexFields.Abstractions` ships in English, Japanese, Simplified Chinese and Traditional Chinese. A required field without a value fails with the `Validate:Required` message.

## Composite Field Types

`Matrix` and `Table` are **composite**: their configuration declares further fields **inline**, so a field definition is a tree rather than a flat record.

* **Matrix** (`Matrix.BlockTypes`): A list of `MatrixBlockType`s, each with a `Name`, a `DisplayName` and its `Fields`. The value is a list of blocks, each of one block type: `[{ "blockTypeName": "quote", "values": { "text": "...", "author": "..." } }]`. Use it for content that is a sequence of different kinds of parts.
* **Table** (`Table.Columns`): A list of `Fields` shared by every row. The value is a list of rows: `[{ "values": { "name": "...", "quantity": 3 } }]`. Use it for repeated records of one shape.

An inline field is an `InlineFieldDefinition`: a `Name`, a `DisplayName`, a `Description`, a `FieldTypeName`, its own `Configuration` and a `Required` flag. Every inline value is validated by its own field type, and an error names the block or row it is in.

Both types implement `ICompositeFieldType`, whose `GetInlineFields(configuration)` returns the inline fields. Neither is indexed: a list of objects has no typed value to search for.

### Nesting

A top-level field may be composite, and so may its inline fields; what those declare must be scalar. `CompositeFieldNesting.MaxDepth` is 3. FlexFields does not enforce the limit by itself; check a configuration a client sends before saving the field definition:

````csharp
if (CompositeFieldNesting.ExceedsMaxDepth(input.FieldTypeName, input.Configuration, _fieldTypeResolver.GetAll()))
{
    throw new UserFriendlyException("The field nests composite types too deeply.");
}
````

### Normalizing Values

Composite values are stored with camelCase keys (`blockTypeName`, `values`), which every reader, the Angular components included, expects. `Validate` reads a composite value case-insensitively, but it only returns errors, so a value with other casing would pass and be stored as it was received.

The two composite types implement `INormalizesValue`. Rewrite the submitted values to their canonical shape **before** validating and saving them:

````csharp
foreach (var flexField in await _flexFieldProvider.GetFlexFieldsAsync(product))
{
    if (_fieldTypeResolver.Get(flexField.FieldTypeName) is INormalizesValue normalizer)
    {
        product.SetField(flexField.Name, normalizer.Normalize(flexField.Value));
    }
}
````

## Resolving Field Types

`IFieldTypeResolver` resolves a field type by its name with `Get(name)`, and returns every registered field type with `GetAll()`, for a field designer that offers them to choose from. Field types are registered automatically, as transient services.

## See Also

* [Creating a Field Type](custom-field-types.md)
* [Querying by Field Values](querying.md)
