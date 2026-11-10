using Localization.Resources.AbpUi;
using Dignite.Drive.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class DriveHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(DriveHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<DriveResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });

        // A business exception is 403 by default; these are a missing thing and a conflict.
        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            options.Map(DriveErrorCodes.ContainerNotFound, HttpStatusCode.NotFound);
            options.Map(DriveErrorCodes.BlobNotFound, HttpStatusCode.NotFound);
            options.Map(DriveErrorCodes.BlobAlreadyExists, HttpStatusCode.Conflict);
            options.Map(DriveErrorCodes.InvalidTicket, HttpStatusCode.Forbidden);
        });
    }
}
