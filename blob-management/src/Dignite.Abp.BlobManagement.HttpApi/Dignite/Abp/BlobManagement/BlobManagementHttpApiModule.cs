using Localization.Resources.AbpUi;
using Dignite.Abp.BlobManagement.Localization;
using Dignite.Abp.BlobManagement.RateLimiting;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class BlobManagementHttpApiModule : AbpModule
{
    // blob-storing's error codes (Dignite.Abp.BlobStoring.Pipeline, .Imaging), published contracts.
    private const string BlobStoringContentTooLarge = "Dignite.Abp.BlobStoring:ContentTooLarge";
    private const string BlobStoringContentTypeNotAllowed = "Dignite.Abp.BlobStoring:ContentTypeNotAllowed";
    private const string BlobStoringContentTypeMismatch = "Dignite.Abp.BlobStoring:ContentTypeMismatch";
    private const string BlobStoringImageTooLarge = "Dignite.Abp.BlobStoring.Imaging:ImageTooLarge";
    private const string BlobStoringImageTooSmall = "Dignite.Abp.BlobStoring.Imaging:ImageTooSmall";
    private const string BlobStoringImageProcessingFailed = "Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed";
    private const string BlobStoringImageDecodeTimeout = "Dignite.Abp.BlobStoring.Imaging:ImageDecodeTimeout";

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(BlobManagementHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The anonymous addresses' limits, ASP.NET Core's rate limiter's policies. They act where the host runs the
        // limiter after routing; a module cannot place middleware there itself.
        context.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(BlobManagementRateLimiting.PublicReads, new BlobManagementRateLimiterPolicy(BlobManagementRateLimiting.PublicReads));
            options.AddPolicy(BlobManagementRateLimiting.Uploads, new BlobManagementRateLimiterPolicy(BlobManagementRateLimiting.Uploads));
            options.AddPolicy(BlobManagementRateLimiting.ReadLinks, new BlobManagementRateLimiterPolicy(BlobManagementRateLimiting.ReadLinks));
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<BlobManagementResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });

        // A business exception is 403 by default; these are a missing thing, a conflict, a bad request or content the
        // module cannot keep.
        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            options.Map(BlobManagementErrorCodes.ContainerNotFound, HttpStatusCode.NotFound);
            options.Map(BlobManagementErrorCodes.BlobNotFound, HttpStatusCode.NotFound);
            options.Map(BlobManagementErrorCodes.BlobAlreadyExists, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.UploadTicketInvalid, HttpStatusCode.Forbidden);
            options.Map(BlobManagementErrorCodes.UploadTicketUsed, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.FilePending, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.ReadLinkInvalid, HttpStatusCode.Forbidden);
            options.Map(BlobManagementErrorCodes.DirectoryNotFound, HttpStatusCode.NotFound);
            options.Map(BlobManagementErrorCodes.DirectoryNameAlreadyExists, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.DirectoryNotEmpty, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.DirectoriesNotAllowed, HttpStatusCode.BadRequest);
            options.Map(BlobManagementErrorCodes.DirectoryCannotMoveIntoItself, HttpStatusCode.BadRequest);
            options.Map(BlobManagementErrorCodes.DirectoryTooDeep, HttpStatusCode.BadRequest);
            options.Map(BlobManagementErrorCodes.SortingNotSupported, HttpStatusCode.BadRequest);
            options.Map(BlobManagementErrorCodes.FileAddressedByName, HttpStatusCode.Conflict);
            options.Map(BlobManagementErrorCodes.StorageQuotaExceeded, HttpStatusCode.InsufficientStorage);

            // What the container's pipeline refuses in an upload, by the codes blob-storing publishes (this layer does
            // not reference its packages): the request's content, not the caller's rights, so not 403.
            options.Map(BlobStoringContentTooLarge, HttpStatusCode.RequestEntityTooLarge);
            options.Map(BlobStoringContentTypeNotAllowed, HttpStatusCode.UnsupportedMediaType);
            options.Map(BlobStoringContentTypeMismatch, HttpStatusCode.BadRequest);
            options.Map(BlobStoringImageTooLarge, HttpStatusCode.BadRequest);
            options.Map(BlobStoringImageTooSmall, HttpStatusCode.BadRequest);
            options.Map(BlobStoringImageProcessingFailed, HttpStatusCode.UnprocessableEntity);
            options.Map(BlobStoringImageDecodeTimeout, HttpStatusCode.UnprocessableEntity);
        });
    }
}
