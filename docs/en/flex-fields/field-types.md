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
| `Number.Placeholder` | `Placeholder` | `null` | The placeholder of the input. |

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

## Checking a Configuration

A configuration is a dictionary, and a dictionary accepts any key. A client that writes `Text.Multiline` where `Text.Mode` was meant is not told: the value is stored, nothing reads it, and the field behaves as if it had never been set. A field type can declare the keys it understands in `IFieldType.ConfigurationKeys`, and `IFieldConfigurationValidator` checks a configuration against them. The eight built-in field types and the [CKEditor field type](ckeditor.md) declare theirs.

FlexFields has no application service, so, like the [nesting limit](#nesting), the check is made by your application, where it accepts a field definition from a client. On a create:

````csharp
public virtual async Task<ProductFieldDto> CreateAsync(CreateProductFieldDto input)
{
    var configuration = input.Configuration.ToConfiguration();

    _configurationValidator.Check(input.FieldTypeName, configuration);

    // ... save the field definition
}
````

On an update, pass the stored configuration as well. A client that only renames a field sends the stored configuration back with the new name, and a definition saved before a key was declared must not be refused for something the client did not touch:

````csharp
public virtual async Task<ProductFieldDto> UpdateAsync(Guid id, UpdateProductFieldDto input)
{
    var field = await _fieldRepository.GetAsync(id);
    var configuration = input.Configuration.ToConfiguration();

    _configurationValidator.Check(field.FieldTypeName, configuration, previous: field.Configuration);

    // ... save the field definition
}
````

With `previous`, a key that has the value it had is not checked again, and neither is an inline field that is exactly as it was; everything the client added or changed is. Pass it only when the field type is the same as before: a configuration moved to another field type is all new.

`Check` throws an `AbpValidationException` that carries every problem at once, so that a client can fix them in one round trip. `Validate` returns the same problems as `ValidationResult`s instead, with the member name `Configuration[key]`, for an application that surfaces them its own way. A message names the key and the field type, and for an unknown key lists the valid ones:

````text
'Text.Multiline' is not a configuration key of the Text field type. The valid keys are: 'Text.Mode', 'Text.CharLimit', 'Text.Placeholder'.
````

The check refuses:

* **An unknown key.** Keys are case-sensitive, as they are stored; when a key differs from a valid one only in case, the message names the valid spelling.
* **A value of the wrong kind**, such as `"many"` for `Text.CharLimit`, `"MultipleLine"` for `Text.Mode` or a list of strings for `Select.Options`. The message says what the key takes and what it was given.
* **A missing required key**, for a field type that declares one. No built-in key is required.

The check accepts only what the configuration classes can read, because storing anything else would only move the failure to the first read of the field:

| Kind | Accepted |
|---|---|
| `String` | A string. |
| `Integer` | A whole number that fits in 32 bits, or a string holding one. The Angular `Number` editor sends the text it was typed as. |
| `Number` | A number, or a string holding one. |
| `Boolean` | `true` or `false`, or a string holding one. |
| `DateTime` | An ISO 8601 date or date-time string, such as `2026-01-31`, `2026-01-31T10:00:00Z` or `2026-01`. |
| `Guid` | A GUID string. |
| `Enum` | An integer that is a member of the enum, such as `1` for `TextMode.MultipleLine`. A member's name is **not** read back, so `"MultipleLine"` is refused. |
| `Array`, `Object` | A JSON array or object. When the key names a type, such as `Select.Options`, the value has to deserialize into it. |

`null` always means that the key is not set. For a key declared with `EmptyStringIsUnset`, such as `Number.Max` or `DateTime.Min`, an empty string means that too: the Angular editors send one for an empty box. For a required string key an empty string counts as missing.

Only what a client sends is checked. **Reading is never validated**: a configuration saved before a key was declared, with a key that is not declared, or a value of another kind, stays readable. Do not call the validator on stored definitions.

* A field type that declares nothing, which is every field type written before `ConfigurationKeys` existed and any that does not override it, is **not checked**: whatever its configuration holds is accepted. This is not the same as declaring an empty list, which says that the field type takes no configuration at all and refuses every key.
* A field type name that is not registered is not checked either: whether your application may save a definition of a type it does not know is its own decision.
* The inline fields of a [composite type](#composite-field-types) are checked whether or not the type declares its own keys, against their own field types, to the depth of `CompositeFieldNesting.MaxDepth`. The message names the inline field the problem is in. An inline field that names a field type that is not registered is refused too, and the message lists the registered ones: nothing else sees it while the definition is saved.
* A composite type's configuration that its inline fields cannot be read from is refused, unless one of its own keys has already said so.

### Describing the Keys

The declaration is also what a client can ask for before it writes, such as a field designer or an AI assistant that cannot read your source code. Each `FieldConfigurationKey` has:

| Member | Description |
|---|---|
| `Name` | The key, as stored. |
| `ValueKind` | The kind of the value, from the table above. |
| `Shape` | The value written as JSON shows it: `integer`, `boolean`, `[{ "text": string, "value": string, "selected": boolean }]`. For an enum, the integer followed by its members, such as `0 (SingleLine)`. |
| `EnumMembers` | The members of an `Enum` key, each with its integer `Value` and its `Name`. |
| `DefaultValue` | What the field behaves as when the key is absent, or `null` when there is no fixed default. An enum's default is its integer. |
| `Description` | What the key is for, in a sentence. |
| `Required` | Whether the field type cannot work without the key. |
| `EmptyStringIsUnset` | Whether an empty string means that the key is not set, for a number, a date or a GUID. |

Return them from the application service that lists the field types, next to the name and whether the type is indexable:

````csharp
var fieldTypes = _fieldTypeResolver.GetAll().Select(fieldType => new FieldTypeDto
{
    Name = fieldType.Name,
    Indexable = fieldType.IsIndexable(),
    ConfigurationKeys = fieldType.ConfigurationKeys?
        .Select(key => new FieldConfigurationKeyDto
        {
            Name = key.Name,
            Type = key.Shape,
            Description = key.Description,
            DefaultValue = key.DefaultValue,
            Required = key.Required,
        })
        .ToList(),
});
````

A `null` list means the field type declares nothing, and an empty list that it takes no configuration; keep the two apart in what you return.

## Resolving Field Types

`IFieldTypeResolver` resolves a field type by its name with `Get(name)`, and returns every registered field type with `GetAll()`, for a field designer that offers them to choose from. Field types are registered automatically, as transient services.

## See Also

* [Creating a Field Type](custom-field-types.md)
* [Querying by Field Values](querying.md)
