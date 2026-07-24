using System;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.BlobStoring;

namespace Dignite.FileExplorer.Files;

public class FileUploadSizeLimitFilter : IAsyncResourceFilter
{
    private const long MultipartOverheadBytes = 64 * 1024;
    private readonly IBlobContainerConfigurationProvider _configurationProvider;

    public FileUploadSizeLimitFilter(IBlobContainerConfigurationProvider configurationProvider)
    {
        _configurationProvider = configurationProvider;
    }

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var containerName = context.HttpContext.Request.Query["containerName"].ToString();
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            var maxFileSizeInBytes = _configurationProvider
                .Get(containerName)
                .GetFileSizeLimitConfiguration()
                .MaxFileSizeInBytes;

            if (maxFileSizeInBytes > 0)
            {
                var requestBodySizeLimit = checked(maxFileSizeInBytes + MultipartOverheadBytes);
                var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false })
                {
                    feature.MaxRequestBodySize = requestBodySizeLimit;
                }
            }
        }

        await next();
    }
}
