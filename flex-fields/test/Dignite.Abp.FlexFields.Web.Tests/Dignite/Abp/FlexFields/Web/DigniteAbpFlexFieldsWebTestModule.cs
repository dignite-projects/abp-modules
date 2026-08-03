using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.Web;

[DependsOn(
    typeof(AbpTestBaseModule),
    typeof(AbpAutofacModule),
    typeof(FlexFieldsWebModule)
    )]
public class DigniteAbpFlexFieldsWebTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Neither is registered by any of the depended-on modules - a real host gets both for free
        // from Microsoft.AspNetCore.Hosting (WebApplication.CreateBuilder()/IWebHostBuilder), which
        // this bare AbpIntegratedTest generic host never runs. RazorViewEngine needs the
        // DiagnosticListener; RazorPartialRenderer needs the HttpContextAccessor to build an
        // ActionContext for it.
        var diagnosticListener = new DiagnosticListener("Dignite.Abp.FlexFields.Web.Tests");
        context.Services.AddSingleton(diagnosticListener);
        context.Services.AddSingleton<DiagnosticSource>(diagnosticListener);

        context.Services.AddHttpContextAccessor();
    }
}
