using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.FileExplorer;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Dignite.FileExplorer.Files;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dignite.Abp.FlexFields.Demo.Data;

/// <summary>
/// Seeds nine <see cref="ProductField"/> definitions - one per built-in field type plus the
/// FileExplorer and CKEditor bolt-ons - and five <see cref="Product"/>s using the built-in ones, so a
/// developer who runs this demo for the first time sees flex fields working immediately instead of an
/// empty database.
/// </summary>
public class ProductDemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IFlexFieldRepository<ProductField> _fieldRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IFlexFieldIndexManager<Product> _indexManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly FileDescriptorManager _fileDescriptorManager;

    public ProductDemoDataSeedContributor(
        IFlexFieldRepository<ProductField> fieldRepository,
        IRepository<Product, Guid> productRepository,
        IFlexFieldIndexManager<Product> indexManager,
        IGuidGenerator guidGenerator,
        FileDescriptorManager fileDescriptorManager)
    {
        _fieldRepository = fieldRepository;
        _productRepository = productRepository;
        _indexManager = indexManager;
        _guidGenerator = guidGenerator;
        _fileDescriptorManager = fileDescriptorManager;
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

        // Not indexable (FileExplorerFieldType.IndexValueType is null) - see CreateSeedImageAsync for
        // why exactly one product gets a real value here rather than every product or none.
        _ = await CreateFieldAsync(
            "images", "Images", FileExplorerFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                [FileExplorerConfigurationNames.FileContainerName] = "images",
                [FileExplorerConfigurationNames.UploadFileMultiple] = true,
            });

        // A real blob in the already-configured "images" container (DemoModule.ConfigureBlobStoring),
        // not a fabricated value pointing at a file that doesn't exist - lets FlexFields.FileExplorer.Web's
        // view render actual name/size/mimeType/url instead of nothing. Only Wireless Mouse gets one, so
        // the demo also shows the "no files" path every other product renders.
        var mouseImages = await CreateSeedImageAsync();

        // Not indexable (CKEditorFieldType.IndexValueType is null, same reasoning as FileExplorer's).
        // ContentFormat = Html (the default) and an images container configured, so this field also
        // exercises the upload-image toolbar button end to end.
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

        var products = new[]
        {
            CreateProduct("Wireless Mouse", ("description", "A comfortable wireless mouse."), ("price", 29.90m),
                ("releaseDate", new DateTime(2025, 3, 1)), ("color", new List<string> { "black", "white" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" }),
                ("images", mouseImages),
                ("content", "<h2>Product Highlights</h2><p><strong>2.4GHz wireless</strong> with up to 6 months of battery life.</p><ul><li>Ergonomic shape</li><li>Silent click buttons</li></ul>"),
                ("notes", mouseNotesMarkdown)),
            CreateProduct("Mechanical Keyboard", ("description", "Tactile switches, RGB backlight."), ("price", 89.00m),
                ("releaseDate", new DateTime(2025, 5, 12)), ("color", new List<string> { "black" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" }),
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
        // ends up empty because none of the six fields had been flushed yet.
        return await _fieldRepository.InsertAsync(field, autoSave: true);
    }

    /// <summary>
    /// Uploads one real, tiny file into the "images" blob container and returns the field value shape
    /// the Angular picker itself writes (see <c>FileExplorerControlComponent.onSelectedFileChange</c>):
    /// the file descriptor's own id/containerName/blobName/name/mimeType/size, denormalized into the
    /// value at pick time - the picker never stores a bare id and re-resolves it later, so seeding does
    /// the same. <c>url</c> is a relative path rather than <see cref="FileDescriptorController"/>'s
    /// absolute one - it has no HttpContext to read a scheme/host from at seed time, and a relative URL
    /// is resolved against the current origin regardless, so it works everywhere the absolute one would.
    /// </summary>
    private async Task<List<object>> CreateSeedImageAsync()
    {
        // A minimal valid 1x1 transparent PNG (67 bytes) - a real, decodable image rather than an
        // arbitrary byte array, so a future thumbnail-rendering view has genuine image bytes behind it.
        const string onePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        await using var stream = new MemoryStream(Convert.FromBase64String(onePixelPngBase64));
        var content = new RemoteStreamContent(stream, "placeholder.png", "image/png");

        // FileDescriptorManager predates nullable reference type annotations (cellName/entityId are
        // [CanBeNull] JetBrains-attributed, not C# `string?`) - the null-forgiving operators below are
        // silencing an inaccurate signature, not asserting non-null values that could actually be null.
        // entityId is cast to string? first because CreateAsync is also overloaded on IEntity entity - a
        // bare null there is ambiguous between the two overloads.
        var file = await _fileDescriptorManager.CreateAsync("images", content, cellName: null!, directoryId: null, entityId: (string?)null!);

        return new List<object>
        {
            new Dictionary<string, object?>
            {
                ["id"] = file.Id,
                ["containerName"] = file.ContainerName,
                ["blobName"] = file.BlobName,
                ["name"] = file.Name,
                ["mimeType"] = file.MimeType,
                ["size"] = file.Size,
                ["url"] = $"/api/file-explorer/files/{file.ContainerName}/{file.BlobName}",
            },
        };
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
