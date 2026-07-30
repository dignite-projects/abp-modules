using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Switch;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.TreeView;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dignite.Abp.FlexFields.Demo.Data;

/// <summary>
/// Seeds six <see cref="ProductField"/> definitions - one per built-in field type - and five
/// <see cref="Product"/>s using them, so a developer who runs this demo for the first time sees flex
/// fields working immediately instead of an empty database.
/// </summary>
public class ProductDemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IFlexFieldRepository<ProductField> _fieldRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IFlexFieldIndexManager<Product> _indexManager;
    private readonly IGuidGenerator _guidGenerator;

    public ProductDemoDataSeedContributor(
        IFlexFieldRepository<ProductField> fieldRepository,
        IRepository<Product, Guid> productRepository,
        IFlexFieldIndexManager<Product> indexManager,
        IGuidGenerator guidGenerator)
    {
        _fieldRepository = fieldRepository;
        _productRepository = productRepository;
        _indexManager = indexManager;
        _guidGenerator = guidGenerator;
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
                ["TextEdit.Mode"] = TextMode.MultipleLine,
                ["TextEdit.CharLimit"] = 1024,
            });

        var price = await CreateFieldAsync(
            "price", "Price", NumberFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["NumericEditField.Decimals"] = 2,
                ["NumericEditField.Min"] = 0,
            },
            required: true, searchable: true);

        var releaseDate = await CreateFieldAsync(
            "releaseDate", "Release Date", DateTimeFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["DateEdit.InputMode"] = DateInputMode.Date,
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
                ["Switch.Default"] = true,
            },
            searchable: true);

        var category = await CreateFieldAsync(
            "category", "Category", TreeFieldType.ControlName,
            new FieldConfigurationDictionary
            {
                ["TreeView.Nodes"] = new List<TreeViewNodeItem>
                {
                    new()
                    {
                        Text = "Electronics",
                        Value = "electronics",
                        Children = new List<TreeViewNodeItem>
                        {
                            new() { Text = "Phones", Value = "electronics-phones" },
                            new() { Text = "Computers", Value = "electronics-computers" },
                        },
                    },
                    new()
                    {
                        Text = "Apparel",
                        Value = "apparel",
                        Children = new List<TreeViewNodeItem>
                        {
                            new() { Text = "Tops", Value = "apparel-tops" },
                            new() { Text = "Pants", Value = "apparel-pants" },
                        },
                    },
                },
            },
            searchable: true);

        var products = new[]
        {
            CreateProduct("Wireless Mouse", ("description", "A comfortable wireless mouse."), ("price", 29.90m),
                ("releaseDate", new DateTime(2025, 3, 1)), ("color", new List<string> { "black", "white" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" })),
            CreateProduct("Mechanical Keyboard", ("description", "Tactile switches, RGB backlight."), ("price", 89.00m),
                ("releaseDate", new DateTime(2025, 5, 12)), ("color", new List<string> { "black" }),
                ("inStock", true), ("category", new List<string> { "electronics-computers" })),
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
