```json
//[doc-seo]
{
    "Description": "Create a FlexFields field type of your own: its configuration, validation and searchable values on the server, and its components in the Angular and MVC UIs."
}
```

# Creating a Field Type

A field type of your own is a class that derives from `FieldTypeBase`, in a project that references `Dignite.Abp.FlexFields.Abstractions` only. It is registered automatically, as a transient service, and resolved by its `Name`. The optional [CKEditor field type](ckeditor.md) of this module is built exactly this way.

A field type has **no rendering on the server**: it names a kind of field, not a UI control. Each UI registers its own components for it, under the same name.

## The Field Type

The following example adds a `Color` field type, whose value is a color like `#3366FF`:

````csharp
public class ColorFieldType : FieldTypeBase
{
    public const string ControlName = "MyProject.Color";

    public ColorFieldType()
    {
        LocalizationResource = typeof(MyProjectResource);
    }

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Color"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration)
    {
        return new ColorConfiguration(configuration);
    }

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();
        var value = args.Field.Value?.ToString();

        if (string.IsNullOrWhiteSpace(value))
        {
            if (args.Field.Required)
            {
                errors.Add(new ValidationResult(
                    L["Validate:Required", args.Field.DisplayName],
                    new[] { args.Field.Name }));
            }

            return errors;
        }

        var configuration = new ColorConfiguration(args.Field.Configuration);
        var pattern = configuration.AllowAlpha ? "^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$" : "^#[0-9A-Fa-f]{6}$";
        if (!Regex.IsMatch(value, pattern))
        {
            errors.Add(new ValidationResult(
                L["Validate:InvalidColor", args.Field.DisplayName],
                new[] { args.Field.Name }));
        }

        return errors;
    }
}
````

* **`Name`**: The registration name, which every field definition of this type stores in its `FieldTypeName`. It is **stored data**: choose a stable, prefixed name and never change it.
* **`DisplayName`**: The localized name, for a field designer. `L` localizes from the resource set in `LocalizationResource`, the `FlexFields` resource by default.
* **`IndexValueType`**: The typed slot the type's searchable values go into, or `null` when the type cannot be searched, such as rich text.
* **`Validate`**: Returns the validation errors of a value, with the field's name as the member name. `args.Field` is the [`FlexFieldValue`](index.md#concepts), with the definition, the usage flags and the value. Remember that a value may be a `JsonElement` after a round trip.
* **`GetSearchableValues`**: Decomposes a value into the values to index. The default returns the value itself for a searchable usage with a value; override it for a multi-valued type, as `Select` and `Tree` do, to return one per selected value.

## The Configuration

A configuration class wraps the field's configuration dictionary with typed properties. Prefix the keys with the field type's name; like the name, they are stored data:

````csharp
public class ColorConfiguration : FieldConfigurationBase
{
    public const string AllowAlphaName = "MyProject.Color.AllowAlpha";

    public bool AllowAlpha
    {
        get => ConfigurationDictionary.GetConfiguration(AllowAlphaName, false);
        set => ConfigurationDictionary.SetConfiguration(AllowAlphaName, value);
    }

    public ColorConfiguration(FieldConfigurationDictionary configuration)
        : base(configuration)
    {
    }

    public ColorConfiguration()
    {
    }
}
````

## A Composite Field Type

A field type whose configuration declares inline fields implements `ICompositeFieldType`, and returns them from `GetInlineFields`, so that [nesting](field-types.md#nesting) can be measured. If its values have a canonical shape, also implement `INormalizesValue`; see [Normalizing Values](field-types.md#normalizing-values).

## The Angular Components

Register a `FieldTypeDefinition` with `provideFlexFields`, under the same name, with the components for the roles it supports: **config** (design the field), **control** (edit a value), **view** (display a value) and **search** (filter by it):

````ts
provideFlexFields({
  name: 'MyProject.Color',
  displayNameKey: 'MyProject::FieldType:Color',
  configComponent: ColorConfigComponent,
  controlComponent: ColorControlComponent,
  viewComponent: ColorViewComponent,
  searchComponent: ColorSearchComponent,
});
````

See [Angular UI](angular-ui.md#adding-a-field-type) for the components.

## The MVC View

Ship a partial view at `Views/Shared/FlexFields/MyProject.Color.cshtml`, and a search partial at `Views/Shared/FlexFields/Search/MyProject.Color.cshtml` if the type is searchable. The tag helpers of `Dignite.Abp.FlexFields.Web` find them by the field type name. See [MVC UI](mvc-ui.md#adding-a-field-type).

## See Also

* [Field Types](field-types.md)
* [CKEditor Field Type](ckeditor.md)
