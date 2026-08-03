using System.Globalization;
using System.Threading;
using Dignite.Abp.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.Testing;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>
/// No live Kestrel/TestServer: <see cref="RazorPartialRenderer"/> only needs a real
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> to be current, not an actual in-flight HTTP
/// request, so a synthetic one with <see cref="HttpContext.RequestServices"/> pointed at this test's
/// DI container is enough to exercise the whole render pipeline for real - DI wiring, view discovery
/// via the ApplicationPart for this assembly, and the per-type Razor logic all run for real, not just
/// at compile time.
/// </summary>
public abstract class DigniteAbpFlexFieldsWebTestBase : AbpIntegratedTest<DigniteAbpFlexFieldsWebTestModule>
{
    protected IRazorPartialRenderer Renderer => GetRequiredService<IRazorPartialRenderer>();

    protected DigniteAbpFlexFieldsWebTestBase()
    {
        // Deterministic across machines/CI: the default view templates format numbers/dates with the
        // current culture, and this suite's assertions are written against InvariantCulture output.
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = ServiceProvider
        };
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
