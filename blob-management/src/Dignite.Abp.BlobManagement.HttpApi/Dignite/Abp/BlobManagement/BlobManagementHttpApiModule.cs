using Localization.Resources.AbpUi;
using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class BlobManagementHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(BlobManagementHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<BlobManagementResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });

        // A business exception is 403 by default; these are a missing thing, a conflict or a bad request.
        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            options.Map(BlobManagementErrorCodes.ContainerNotFound, HttpStatusCode.NotFound);
            options.Map(BlobManagementErrorCodes.BlobNotFound, HttpStatusCode.NotFound);
            options.Map(BlobManagementErrorCodes.BlobAlreadyExists, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.InvalidTicket, HttpStatusCode.Forbidden);
        });
    }
}
