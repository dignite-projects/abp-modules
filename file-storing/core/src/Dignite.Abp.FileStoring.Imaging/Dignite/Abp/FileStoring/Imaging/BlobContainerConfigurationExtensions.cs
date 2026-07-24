using System;
using Dignite.Abp.FileStoring;
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
            configureAction(new ImageResizeHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.FileHandlers,
                blobProcessHandlers);
        }
    }
}
