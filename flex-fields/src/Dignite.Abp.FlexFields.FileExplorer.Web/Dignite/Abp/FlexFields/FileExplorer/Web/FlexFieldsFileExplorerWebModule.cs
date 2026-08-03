using Dignite.Abp.AspNetCore.Mvc.Razor;
using Dignite.Abp.FlexFields.Web;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.FileExplorer.Web;

/// <summary>
/// Ships the one view under <c>Views/Shared/FlexFields/FileExplorer.cshtml</c> - depending on this
/// module alone is enough for a host to get both the FileExplorer field type itself
/// (<see cref="FlexFieldsFileExplorerModule"/>) and its SSR rendering, without having to remember to
/// add both separately.
/// </summary>
[DependsOn(
    typeof(FlexFieldsWebModule),
    typeof(FlexFieldsFileExplorerModule)
    )]
public class FlexFieldsFileExplorerWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddCompiledRazorAssemblyPartIfNotExists(typeof(FlexFieldsFileExplorerWebModule).Assembly);
        });
    }
}
