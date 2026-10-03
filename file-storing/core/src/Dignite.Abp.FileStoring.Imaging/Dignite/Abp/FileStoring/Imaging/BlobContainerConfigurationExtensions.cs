using System;
using Dignite.Abp.FileStoring;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;

namespace Dignite.Abp.FileStoring.Imaging;

public static class BlobContainerConfigurationExtensions
{
    public static ImageResizeHandlerConfiguration GetImageResizeConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new ImageResizeHandlerConfiguration(containerConfiguration);
    }

    public static void AddImageResizeHandler(
        this BlobContainerConfiguration containerConfiguration,
        Action<ImageResizeHandlerConfiguration> configureAction)
    {
        var blobProcessHandlers = containerConfiguration.GetConfigurationOrDefault(
            BlobContainerConfigurationNames.FileHandlers,
            new TypeList<IFileHandler>())!;

        if (blobProcessHandlers.TryAdd<ImageResizeHandler>())
        {
            var configuration = new ImageResizeHandlerConfiguration(containerConfiguration);
            configureAction(configuration);

            if (configuration.ImageWidth <= 0 && configuration.ImageHeight <= 0)
            {
                throw new AbpException(
                    $"{nameof(ImageResizeHandler)} requires {nameof(ImageResizeHandlerConfiguration.ImageWidth)}, " +
                    $"{nameof(ImageResizeHandlerConfiguration.ImageHeight)}, or both to be greater than zero.");
            }

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.FileHandlers,
                blobProcessHandlers);
        }
    }
}
