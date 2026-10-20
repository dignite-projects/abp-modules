```json
//[doc-seo]
{
    "Description": "Contribute MCP tools, resources and prompts from an ABP module with AddAbpMcpModule: tool classes, permissions, namespaces, instructions and dynamic resource lists."
}
```

# Contributing Tools

A module contributes to the application's MCP server from a project of its own, conventionally `MyModule.Mcp`, that depends on `Dignite.Abp.AspNetCore.Mcp` and on the module's application contracts. The MCP project only describes the module's application services to an AI assistant; it is another caller of them, not a second implementation.

## Registering the Module

Call `AddAbpMcpModule` in the `ConfigureServices` method of the module's MCP project, with the module's namespace:

````csharp
[DependsOn(
    typeof(MyModuleApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
)]
public class MyModuleMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpMcpModule("my_module", mcp => mcp
            .AddTools<MyModuleTools>()
            .AddResources<MyModuleResources>()
            .AddPrompts<MyModulePrompts>()
            .AddInstructions("Use the my_module_* tools to find and update products. Start with my_module_search_products."));
    }
}
````

* `AddTools<T>()`, `AddResources<T>()` and `AddPrompts<T>()` register every `[McpServerTool]`, `[McpServerResource]` or `[McpServerPrompt]` method of a class.
* `AddInstructions(text)` adds the module's section to the instructions the server gives to the client: what its tools are for, and where to start.
* `UseUriScheme(scheme)` sets the scheme of the module's resource URIs; see [Namespaces](#namespaces).
* `AddResourceListContributor<T>()` adds [dynamic resources](#dynamic-resources) to the resource list.

Calling `AddAbpMcpModule` again with the same name extends the same namespace, which is how another package adds tools to an existing module.

## Writing Tools

A tool class uses the SDK's own attributes, `[McpServerToolType]`, `[McpServerTool]` and `[Description]`, and ASP.NET Core's `[Authorize]`. Register it in [dependency injection](https://abp.io/docs/latest/framework/fundamentals/dependency-injection), for example with `ITransientDependency`: the server resolves it from ABP's container on every call.

````csharp
[McpServerToolType]
public class MyModuleTools : ITransientDependency
{
    private readonly IProductAppService _productAppService;

    public MyModuleTools(IProductAppService productAppService)
    {
        _productAppService = productAppService;
    }

    [McpServerTool(Name = "my_module_search_products", ReadOnly = true)]
    [Description("Searches the product catalog by name. Returns at most 20 products.")]
    [Authorize(MyModulePermissions.Products.Default)]
    public virtual async Task<PagedResultDto<ProductDto>> SearchProductsAsync(
        [Description("A part of the product name.")] string filter)
    {
        return await _productAppService.GetListAsync(new GetProductListInput
        {
            Filter = filter,
            MaxResultCount = 20
        });
    }

    [McpServerTool(Name = "my_module_update_price")]
    [Description("Changes the price of a product.")]
    [Authorize(MyModulePermissions.Products.Edit)]
    public virtual async Task<ProductDto> UpdatePriceAsync(Guid productId, decimal price)
    {
        return await _productAppService.UpdatePriceAsync(productId, price);
    }
}
````

* Call the module's **application services**, so a tool gets the same validation, authorization and business rules as the HTTP API.
* The descriptions are what the model reads to decide which tool to call and how, so write them for it: what the tool does, what it returns, and the limits.
* Put `[Authorize]` on each tool method. A tool the current user has no permission for does not appear in `tools/list`.
* Make the methods `virtual`, so an application can override a tool by deriving from the class and [replacing](https://abp.io/docs/latest/framework/fundamentals/dependency-injection#replace-a-service) it.

### Why Not the SDK's WithTools

Use `AddTools<T>()`, not the SDK's `WithTools<T>()`. Besides recording which namespace owns a tool, `AddTools<T>()` resolves the tool class from ABP's container on every call. The SDK creates it with `ActivatorUtilities` instead, so a replacement registered with `[Dependency(ReplaceServices = true)]` would never be used, and overriding a virtual tool method would silently do nothing.

## Namespaces

`AddAbpMcpModule(name, ...)` claims a namespace, and the server **refuses to start** if anything breaks it:

* Every **tool** and **prompt** name must start with `{name}_`, such as `my_module_search_products`.
* Every **resource** URI must use the module's scheme: the name with `_` turned into `-` by default (`my-module://...`), or the scheme set with `UseUriScheme(...)`.
* Module names are lower-case snake_case, and must not nest: `vault` and `vault_extract` cannot both be registered.
* Two modules may not share a URI scheme.
* A tool, resource or prompt registered outside `AddAbpMcpModule`, with the SDK's own `WithTools<T>()` and so on, is rejected, because no namespace owns it. An application can allow such primitives; see [Third-Party Tool Libraries](hosting.md#third-party-tool-libraries).
* Any duplicate name or URI template fails, naming both registrations.

The rules are checked against a module's own registrations, so a module's tests catch a violation before any application combines it with another module.

## Dynamic Resources

The SDK lists static `[McpServerResource]` registrations by itself. For resources that are the caller's own data, such as one entry per document type, implement `IAbpMcpResourceListContributor` and register it with `AddResourceListContributor<T>()`:

````csharp
public class MyModuleResourceListContributor : IAbpMcpResourceListContributor
{
    private readonly ICategoryAppService _categoryAppService;

    public MyModuleResourceListContributor(ICategoryAppService categoryAppService)
    {
        _categoryAppService = categoryAppService;
    }

    public async Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryAppService.GetListAsync();

        return categories.Items
            .Select(c => new Resource { Uri = $"my-module://categories/{c.Id}", Name = c.Name })
            .ToList();
    }
}
````

* A contributor is resolved from the request scope, and must **authorize** what it lists.
* When the caller may see nothing, return `null` or an empty list rather than throw: an exception fails the listing for every module.
* It may only list URIs in its module's scheme.

Do not use the SDK's `WithListResourcesHandler`: it is one slot on the shared server, and the next module to set it replaces yours.

## See Also

* [Hosting the Server](hosting.md)
* [Errors and Unit of Work](errors-and-unit-of-work.md)
* [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
