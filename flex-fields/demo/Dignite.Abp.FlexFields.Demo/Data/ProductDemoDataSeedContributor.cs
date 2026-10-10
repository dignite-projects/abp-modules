using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Table;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dignite.Abp.FlexFields.Demo.Data;

/// <summary>
/// Seeds ten <see cref="ProductField"/> definitions - one per built-in field type, plus two CKEditor
/// bolt-on ones - and five <see cref="Product"/>s using the built-in ones,
/// so a developer who runs this demo for the first time sees flex fields working immediately instead of
/// an empty database.
/// </summary>
public class ProductDemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>The casing the composite configuration keys are stored in - see <see cref="CanonicalizeConfiguration"/>.</summary>
    private static readonly JsonSerializerOptions WebSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlexFieldRepository<ProductField> _fieldRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IFlexFieldIndexManager<Product> _indexManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IFieldTypeResolver _fieldTypeResolver;

    public ProductDemoDataSeedContributor(
        IFlexFieldRepository<ProductField> fieldRepository,
        IRepository<Product, Guid> productRepository,
        IFlexFieldIndexManager<Product> indexManager,
        IGuidGenerator guidGenerator,
        IFieldTypeResolver fieldTypeResolver)
    {
        _fieldRepository = fieldRepository;
        _productRepository = productRepository;
        _indexManager = indexManager;
        _guidGenerator = guidGenerator;
        _fieldTypeResolver = fieldTypeResolver;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _fieldRepository.GetCountAsync() > 0)
        {
            return;
        }

        var description = await CreateFieldAsync(
            "description", "Description", TextFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["Text.Mode"] = TextMode.MultipleLine,
                ["Text.CharLimit"] = 1024,
            });

        var price = await CreateFieldAsync(
            "price", "Price", NumberFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["Number.Decimals"] = 2,
                ["Number.Min"] = 0,
            },
            required: true, searchable: true);

        var releaseDate = await CreateFieldAsync(
            "releaseDate", "Release Date", DateTimeFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["DateTime.InputMode"] = DateTimeInputMode.Date,
            },
            searchable: true);

        var color = await CreateFieldAsync(
            "color", "Color", SelectFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["Select.Multiple"] = true,
                ["Select.Options"] = new List<SelectListItem>
                {
                    new("Red", "red", false),
                    new("Blue", "blue", false),
                    new("Black", "black", false),
                    new("White", "white", false),
                },
            },
            searchable: true);

        var inStock = await CreateFieldAsync(
            "inStock", "In Stock", BooleanFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["Boolean.Default"] = true,
            },
            searchable: true);

        var category = await CreateFieldAsync(
            "category", "Category", TreeFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["Tree.Nodes"] = new List<TreeNodeItem>
                {
                    new()
                    {
                        Text = "Electronics",
                        Value = "electronics",
                        Children = new List<TreeNodeItem>
                        {
                            new() { Text = "Phones", Value = "electronics-phones" },
                            new() { Text = "Computers", Value = "electronics-computers" },
                        },
                    },
                    new()
                    {
                        Text = "Apparel",
                        Value = "apparel",
                        Children = new List<TreeNodeItem>
                        {
                            new() { Text = "Tops", Value = "apparel-tops" },
                            new() { Text = "Pants", Value = "apparel-pants" },
                        },
                    },
                },
            },
            searchable: true);

        // The two composite built-ins. Neither is indexable (both IndexValueType are null - a value that
        // is a list of composite objects has no typed index column to decompose into), so neither can be
        // marked searchable, exactly like the two bolt-ons below.
        var specs = await CreateFieldAsync(
            "specs", "Specifications", TableFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                // One column schema shared by every row - contrast Matrix's several named block types.
                [TableConfigurationNames.Columns] = CanonicalizeConfiguration(new List<InlineFieldDefinition>
                {
                    new()
                    {
                        Name = "name",
                        DisplayName = "Name",
                        FieldTypeName = TextFieldType.ControlName,
                        Required = true,
                    },
                    new()
                    {
                        Name = "value",
                        DisplayName = "Value",
                        FieldTypeName = TextFieldType.ControlName,
                    },
                }),
            });

        var sections = await CreateFieldAsync(
            "sections", "Content Sections", MatrixFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                [MatrixConfigurationNames.BlockTypes] = CanonicalizeConfiguration(new List<MatrixBlockType>
                {
                    new()
                    {
                        Name = "paragraph",
                        DisplayName = "Paragraph",
                        Fields = new List<InlineFieldDefinition>
                        {
                            new()
                            {
                                Name = "title",
                                DisplayName = "Title",
                                FieldTypeName = TextFieldType.ControlName,
                                Required = true,
                            },
                            new()
                            {
                                Name = "body",
                                DisplayName = "Body",
                                FieldTypeName = TextFieldType.ControlName,
                                Configuration = new FieldConfigurationDictionary
                                {
                                    [TextConfigurationNames.Mode] = TextMode.MultipleLine,
                                },
                            },
                        },
                    },
                    new()
                    {
                        Name = "callout",
                        DisplayName = "Callout",
                        Fields = new List<InlineFieldDefinition>
                        {
                            new()
                            {
                                Name = "text",
                                DisplayName = "Text",
                                FieldTypeName = TextFieldType.ControlName,
                                Required = true,
                            },
                            // A nested field type with a configuration of its own, not just another Text:
                            // the sub-field's own type resolves and renders through the same dispatch the
                            // top level uses, configuration and all.
                            new()
                            {
                                Name = "level",
                                DisplayName = "Level",
                                FieldTypeName = SelectFieldType.ControlName,
                                Configuration = new FieldConfigurationDictionary
                                {
                                    [SelectConfigurationNames.Options] = new List<SelectListItem>
                                    {
                                        new("Info", "info", false),
                                        new("Warning", "warning", false),
                                    },
                                },
                            },
                        },
                    },
                }),
            });

        // Not indexable (CKEditorFieldType.IndexValueType is null). ContentFormat = Html (the default)
        // and an images container configured, so the upload-image toolbar button appears as soon as the
        // host gives the Angular control an upload API (this demo has none - see app.config.ts).
        _ = await CreateFieldAsync(
            "content", "Content", CKEditorFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                [CKEditorConfigurationNames.Mode] = CKEditorMode.Full,
                [CKEditorConfigurationNames.ContentFormat] = CKEditorContentFormat.Html,
                [CKEditorConfigurationNames.ImagesContainerName] = "images",
            });

        // ContentFormat = Markdown, Mode = Full, with an images container configured: Full must support
        // image upload regardless of content format, exactly like "content" above but exercising the
        // Markdown data processor + Image/ImageUpload plugin combination specifically (GFM serializes an
        // uploaded image as "![](url)" - see FlexFieldsCKEditorWebModule for the read-back side).
        _ = await CreateFieldAsync(
            "notes", "Notes (Markdown)", CKEditorFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                [CKEditorConfigurationNames.Mode] = CKEditorMode.Full,
                [CKEditorConfigurationNames.ContentFormat] = CKEditorContentFormat.Markdown,
                [CKEditorConfigurationNames.ImagesContainerName] = "images",
            });

        // GFM-specific syntax on purpose (strikethrough, a pipe table): a plain CommonMark converter
        // would render these two constructs as literal source text instead of <del>/<table> markup, so
        // this is a direct check that the server's Markdig pipeline actually has .UseAdvancedExtensions()
        // wired up (see FlexFieldsCKEditorWebModule) and that the client's `marked` call has GFM on
        // (its default).
        const string mouseNotesMarkdown =
            "## Care Instructions\n\n" +
            "Hand wash only. **Do not** tumble dry.\n\n" +
            "- Keep away from direct sunlight\n" +
            "- Store in a dry place\n\n" +
            "~~Battery lasts 3 months~~ Battery lasts 6 months.\n\n" +
            "| Step | Action |\n| --- | --- |\n| 1 | Unbox |\n| 2 | Pair via Bluetooth |";

        // Composite values, authored as the CLR shape a fresh in-memory write produces - a live
        // List<TableRow>/List<MatrixBlockValue> - then canonicalized, see NormalizeCompositeValue.
        var mouseSpecs = NormalizeCompositeValue(TableFieldType.ControlName, new List<TableRow>
        {
            new() { Values = new FlexFieldDictionary { ["name"] = "Connectivity", ["value"] = "2.4GHz wireless" } },
            new() { Values = new FlexFieldDictionary { ["name"] = "Battery life", ["value"] = "6 months" } },
            new() { Values = new FlexFieldDictionary { ["name"] = "Weight", ["value"] = "78 g" } },
        });

        // Two block types in one value, so the Matrix view has to dispatch per block rather than render a
        // single fixed schema - and the callout's "level" sub-field exercises a nested Select.
        var mouseSections = NormalizeCompositeValue(MatrixFieldType.ControlName, new List<MatrixBlockValue>
        {
            new()
            {
                BlockTypeName = "paragraph",
                Values = new FlexFieldDictionary
                {
                    ["title"] = "In the box",
                    ["body"] = "The mouse, a USB-A receiver, and one AA battery.\nNo cable is included.",
                },
            },
            new()
            {
                BlockTypeName = "callout",
                Values = new FlexFieldDictionary
                {
                    ["text"] = "Pair the receiver before first use.",
                    ["level"] = "info",
                },
            },
        });

        var keyboardSpecs = NormalizeCompositeValue(TableFieldType.ControlName, new List<TableRow>
        {
            new() { Values = new FlexFieldDictionary { ["name"] = "Switches", ["value"] = "Hot-swappable tactile" } },
            new() { Values = new FlexFieldDictionary { ["name"] = "Layout", ["value"] = "87-key TKL" } },
        });

        var keyboardSections = NormalizeCompositeValue(MatrixFieldType.ControlName, new List<MatrixBlockValue>
        {
            new()
            {
                BlockTypeName = "callout",
                Values = new FlexFieldDictionary
                {
                    ["text"] = "RGB profiles are stored on the keyboard, not the driver.",
                    ["level"] = "warning",
                },
            },
        });

        var products = new[]
        {
            CreateProduct("Wireless Mouse", ("description", "A comfortable wireless mouse."), ("price", 29.90m),
                ("releaseDate", new DateTime(2025, 3, 1)), ("color", new List<string> { "black", "white" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" }),
                ("specs", mouseSpecs), ("sections", mouseSections),
                ("content", "<h2>Product Highlights</h2><p><strong>2.4GHz wireless</strong> with up to 6 months of battery life.</p><ul><li>Ergonomic shape</li><li>Silent click buttons</li></ul>"),
                ("notes", mouseNotesMarkdown)),
            CreateProduct("Mechanical Keyboard", ("description", "Tactile switches, RGB backlight."), ("price", 89.00m),
                ("releaseDate", new DateTime(2025, 5, 12)), ("color", new List<string> { "black" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" }),
                ("specs", keyboardSpecs), ("sections", keyboardSections),
                ("content", "<h2>Product Highlights</h2><p>Hot-swappable <strong>mechanical switches</strong> with per-key RGB.</p>")),
            CreateProduct("Smartphone X", ("description", "Flagship smartphone, 128GB."), ("price", 699.00m),
                ("releaseDate", new DateTime(2025, 9, 20)), ("color", new List<string> { "black", "blue" }),
                ("inStock", false), ("category", new List<string> { "electronics-phones" })),
            CreateProduct("Cotton T-Shirt", ("description", "Everyday cotton t-shirt."), ("price", 15.50m),
                ("releaseDate", new DateTime(2025, 1, 15)), ("color", new List<string> { "white", "red", "blue" }),
                ("inStock", true), ("category", new List<string> { "apparel-tops" })),
            CreateProduct("Denim Jeans", ("description", "Slim-fit denim jeans."), ("price", 45.00m),
                ("releaseDate", new DateTime(2025, 2, 10)), ("color", new List<string> { "blue" }),
                ("inStock", true), ("category", new List<string> { "apparel-pants" })),
        };

        foreach (var product in products)
        {
            await _productRepository.InsertAsync(product);

            // Without this, the search page (issue #3 step 2's whole point) would be empty until the
            // first edit of each product triggered a synchronize.
            await _indexManager.SynchronizeAsync(product);
        }
    }

    private async Task<ProductField> CreateFieldAsync(
        string name,
        string displayName,
        string fieldTypeName,
        FieldConfigurationDictionary configuration,
        bool required = false,
        bool searchable = false)
    {
        var field = new ProductField(_guidGenerator.Create(), name, displayName, fieldTypeName)
        {
            Configuration = configuration,
            Required = required,
            Searchable = searchable,
        };

        // autoSave: true - ProductFlexFieldProvider.GetFlexFieldsAsync reads ProductFields with a real
        // query, not the change tracker, so the field must actually be committed before SynchronizeAsync
        // (called below, once per seeded product) can see it. Without this, every seeded product's index
        // ends up empty because none of the ten fields had been flushed yet.
        return await _fieldRepository.InsertAsync(field, autoSave: true);
    }

    /// <summary>
    /// Runs a composite value through its field type's own <see cref="INormalizesValue.Normalize"/>, the
    /// way <c>ProductAppService</c> does on every write. This seeder goes straight to the repository and
    /// so bypasses that app service, and it cannot skip the step: EF's <c>AbpJsonValueConverter</c>
    /// serializes the bag with default <see cref="System.Text.Json.JsonSerializerOptions"/>, which would
    /// persist <c>MatrixBlockValue</c>/<c>TableRow</c> under their PascalCase CLR property names. The
    /// server's own readers are case-insensitive and would not notice, but the Angular controls read the
    /// camelCase wire shape and would render nothing - exactly the failure <see cref="INormalizesValue"/>
    /// exists to prevent.
    /// </summary>
    private object NormalizeCompositeValue(string fieldTypeName, object value)
    {
        return ((INormalizesValue)_fieldTypeResolver.Get(fieldTypeName)).Normalize(value)!;
    }

    /// <summary>
    /// The <see cref="NormalizeCompositeValue"/> problem one level up, for a composite field's
    /// <i>configuration</i>: <c>Table.Columns</c> and <c>Matrix.BlockTypes</c> hold whole field
    /// definitions, and handing the <see cref="FieldConfigurationDictionary"/> a live
    /// <c>List&lt;InlineFieldDefinition&gt;</c>/<c>List&lt;MatrixBlockType&gt;</c> makes EF's
    /// <c>AbpJsonValueConverter</c> - which serializes with default
    /// <see cref="JsonSerializerOptions"/> - persist them under their PascalCase CLR property names.
    /// The canonical stored shape for these two keys is camelCase (<c>{name, displayName, description,
    /// fieldTypeName, required, configuration}</c>): it is what the Angular designer writes, and what
    /// existing Dignite.Site data already holds. Serializing to a <see cref="JsonElement"/> with
    /// <see cref="JsonSerializerDefaults.Web"/> here stores exactly that, while keeping the typed
    /// object literals at the call site readable.
    ///
    /// <para>
    /// Nothing is broken without it - <see cref="FieldConfigurationDictionaryExtensions.GetConfiguration{TConfiguration}"/>
    /// round-trips through <see cref="JsonSerializerDefaults.Web"/> and so reads either casing, and the
    /// Angular readers are tolerant too. This is about the demo seeding the shape hosts should copy,
    /// exactly as it already does for composite <i>values</i>.
    /// </para>
    ///
    /// <para>
    /// Only the two composite keys get this. <c>Select.Options</c> and <c>Tree.Nodes</c> are the
    /// opposite case: PascalCase (<c>Text</c>/<c>Value</c>/<c>Selected</c>) is <i>their</i> canonical
    /// shape - it is what the Angular designer writes for them - so those two are deliberately left
    /// unwrapped above.
    /// </para>
    ///
    /// <para>
    /// One wrinkle follows from that: an inline field's own <c>Configuration</c> sits inside this
    /// subtree, so a <c>Select.Options</c> nested in a block type is re-cased along with everything
    /// around it. Harmless - <see cref="Dignite.Abp.FlexFields.Select.SelectConfiguration.Options"/>
    /// round-trips through <see cref="JsonSerializerDefaults.Web"/> and the client's
    /// <c>normalizeSelectListItem</c> reads either casing - and the configuration <i>keys</i> survive
    /// untouched either way (<c>"Select.Options"</c>, <c>"Text.Mode"</c>), because
    /// <see cref="JsonSerializerDefaults.Web"/> sets a property naming policy, not a dictionary-key one.
    /// </para>
    /// </summary>
    private static JsonElement CanonicalizeConfiguration(object configurationValue)
    {
        return JsonSerializer.SerializeToElement(configurationValue, WebSerializerOptions);
    }

    private Product CreateProduct(string name, params (string FieldName, object Value)[] values)
    {
        var product = new Product(_guidGenerator.Create(), name);

        foreach (var (fieldName, value) in values)
        {
            product.SetField(fieldName, value);
        }

        return product;
    }
}
