```json
//[doc-seo]
{
    "Description": "Render FlexFields values and search inputs in MVC / Razor Pages with the flex-field-view and flex-field-search tag helpers, and override the partial view of a field type."
}
```

# MVC UI

The `Dignite.Abp.FlexFields.Web` package renders field values and search inputs on the server, in [MVC / Razor Pages](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/overall), for every built-in field type: the server-side counterpart of the Angular package's view and search components.

It has two tag helpers and no forms: there are no configuration or edit tag helpers. Use it to show fields on public pages, lists and detail pages rendered on the server.

## Installation

Install the `Dignite.Abp.FlexFields.Web` package into your `Web` project, depend on `AbpFlexFieldsWebModule`, and add its tag helpers to your `_ViewImports.cshtml`:

````cshtml
@addTagHelper *, Dignite.Abp.FlexFields.Web
````

## Displaying Values

`<flex-field-view>` renders the value of one `FlexFieldValue`, which your page model gets from `IFlexFieldProvider<TEntity>.GetFlexFieldsAsync(entity)`:

````cshtml
@foreach (var field in Model.FlexFields)
{
    <flex-field-view field="@field" />
}
````

* `field` (`FlexFieldValue`): The field, with its definition and value.
* `show-in-list` (`bool`): A compact rendering, for a list cell.
* `partial-name` (`string`): A partial view to render instead of the conventional one.

The tag helpers render what they are given and look nothing up: assembling the `FlexFieldValue`s is your page model's job.

## Search Inputs

`<flex-field-search>` renders the filter inputs of a searchable field:

````cshtml
<form method="get">
    @foreach (var field in Model.SearchableFields)
    {
        <flex-field-search field="@field" />
    }
    <button type="submit">Search</button>
</form>
````

* `field` (`FlexFieldValue`): The field.
* `name` (`string`): The base name of the form inputs. The field's name by default.
* `partial-name` (`string`): A partial view to render instead of the conventional one.

The inputs are named after the base name: `{Name}` for a text, a select or a tree, `{Name}Min` and `{Name}Max` for a number, `{Name}From` and `{Name}To` for a date. Read them back by those names and turn them into [query conditions](querying.md#query-conditions) for the same `IFlexFieldQueryExecutor<TEntity>` your API uses; the tag helper renders inputs only. `Matrix` and `Table` have no search input.

## Overriding a Field Type's View

The tag helpers render a partial view by the field's **type name**, by convention:

* `Views/Shared/FlexFields/{FieldTypeName}.cshtml` for `<flex-field-view>`, with a `FlexFieldViewModel` (`Field`, `ShowInList`) model.
* `Views/Shared/FlexFields/Search/{FieldTypeName}.cshtml` for `<flex-field-search>`, with a `FlexFieldSearchViewModel` (`Field`, `Name`) model.

The partial is found the way MVC finds any partial view, so a view at the same path in your own project **overrides** the built-in one:

````cshtml
@* Views/Shared/FlexFields/Boolean.cshtml *@
@model Dignite.Abp.FlexFields.Web.FlexFieldViewModel

@if (Model.Field.Value is true || Model.Field.Value?.ToString() == "True")
{
    <span class="badge bg-success">@Model.Field.DisplayName</span>
}
````

### Adding a Field Type

For a [field type of your own](custom-field-types.md), ship its partial views at the same conventional paths, named after its type name. Put them in a small project of their own next to the field type, depending on the field type and on `Dignite.Abp.FlexFields.Web`, as the [CKEditor field type](ckeditor.md) does with `Dignite.Abp.FlexFields.CKEditor.Web`.

Precompiled views of a referenced assembly are not found through ABP's automatic module registration, which adds a plain assembly part. Register the project's views in its module class with `AddCompiledRazorAssemblyPartIfNotExists`, an extension method of `Dignite.Abp.FlexFields.Web`, as the module's own `Web` projects do:

````csharp
public override void PreConfigureServices(ServiceConfigurationContext context)
{
    PreConfigure<IMvcBuilder>(mvcBuilder =>
    {
        mvcBuilder.AddCompiledRazorAssemblyPartIfNotExists(typeof(MyProjectFlexFieldsWebModule).Assembly);
    });
}
````

## See Also

* [Angular UI](angular-ui.md)
* [Querying by Field Values](querying.md)
