using Dignite.Abp.AspNetCore.Mvc.Razor;
using Dignite.Abp.FlexFields.Web;
using Ganss.Xss;
using Markdig;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.CKEditor.Web;

/// <summary>
/// Ships the one view under <c>Views/Shared/FlexFields/CKEditor.cshtml</c>, plus the shared
/// <see cref="HtmlSanitizer"/>/<see cref="MarkdownPipeline"/> singletons that view depends on -
/// depending on this module alone is enough for a host to get the CKEditor field type itself
/// (<see cref="FlexFieldsCKEditorModule"/>), its SSR rendering, and safe HTML output, without wiring
/// three separate things.
/// </summary>
[DependsOn(
    typeof(FlexFieldsWebModule),
    typeof(FlexFieldsCKEditorModule)
    )]
public class FlexFieldsCKEditorWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddCompiledRazorAssemblyPartIfNotExists(typeof(FlexFieldsCKEditorWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(CreateSanitizer());

        // .UseAdvancedExtensions() is required, not cosmetic: CKEditor 5's Markdown plugin emits GFM
        // (pipe tables, ~~strikethrough~~, autolinks), and Markdig's bare default pipeline only
        // understands CommonMark - without this, a Markdown-format field's tables/strikethrough render
        // as literal source text instead of HTML.
        context.Services.AddSingleton(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    }

    /// <summary>
    /// Ganss.Xss's default AllowedTags already cover everything the v1 toolbar
    /// (CKEditorControlComponent's buildEditorConfig) can produce - headings, basic styles, lists,
    /// links, images, tables, blockquote, code blocks, figure/figcaption. The one gap: `class` is not
    /// in the default AllowedAttributes (deliberately, against classjacking - see the package's own
    /// README), but CKEditor 5's own output structurally depends on it: &lt;figure class="image"&gt;,
    /// &lt;figure class="table"&gt;, and &lt;code class="language-xxx"&gt; code-block language hints.
    /// </summary>
    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer;
    }
}
