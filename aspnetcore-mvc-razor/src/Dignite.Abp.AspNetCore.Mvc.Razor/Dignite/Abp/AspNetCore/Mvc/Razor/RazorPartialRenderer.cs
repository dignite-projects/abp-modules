using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mvc.Razor;

public class RazorPartialRenderer : IRazorPartialRenderer, ITransientDependency
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RazorPartialRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> RenderAsync<TModel>(string partialName, TModel model)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                $"{nameof(RazorPartialRenderer)} needs a current HttpContext - it can only run while a real HTTP request is being handled.");

        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());
        var view = FindView(actionContext, partialName);

        using var output = new StringWriter();

        var viewContext = new ViewContext(
            actionContext,
            view,
            new ViewDataDictionary<TModel>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = model
            },
            new TempDataDictionary(httpContext, _tempDataProvider),
            output,
            new HtmlHelperOptions());

        await view.RenderAsync(viewContext);
        return output.ToString();
    }

    /// <summary>
    /// Mirrors what <c>&lt;partial name="..." /&gt;</c> does internally: try <paramref name="partialName"/>
    /// as a rooted/relative path first, then fall back to the view engine's normal conventional lookup
    /// (current controller/page, then <c>Views/Shared</c>).
    /// </summary>
    private IView FindView(ActionContext actionContext, string partialName)
    {
        var pathResult = _viewEngine.GetView(executingFilePath: null, viewPath: partialName, isMainPage: false);
        if (pathResult.Success)
        {
            return pathResult.View;
        }

        var conventionResult = _viewEngine.FindView(actionContext, partialName, isMainPage: false);
        if (conventionResult.Success)
        {
            return conventionResult.View;
        }

        var searchedLocations = pathResult.SearchedLocations.Concat(conventionResult.SearchedLocations);
        throw new InvalidOperationException(
            $"Partial view '{partialName}' was not found. The following locations were searched:{Environment.NewLine}{string.Join(Environment.NewLine, searchedLocations)}");
    }
}
