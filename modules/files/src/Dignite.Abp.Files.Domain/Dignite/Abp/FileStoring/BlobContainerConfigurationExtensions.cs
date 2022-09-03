using System;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;


namespace Dignite.Abp.FileStoring;

public static class BlobContainerConfigurationExtensions
{

    #region Blob size limit handler configuration extenisions
    public static BlobSizeLimitHandlerConfiguration GetBlobSizeLimitConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new BlobSizeLimitHandlerConfiguration(containerConfiguration);
    }

    public static void AddBlobSizeLimitHandler(
        this BlobContainerConfiguration containerConfiguration,
        Action<BlobSizeLimitHandlerConfiguration> configureAction)
    {
        var blobProcessHandlers = containerConfiguration.GetConfigurationOrDefault(
            BlobContainerConfigurationNames.BlobHandlers,
            new TypeList<IBlobHandler>());


        if (blobProcessHandlers.TryAdd<BlobSizeLimitHandler>())
        {
            configureAction(new BlobSizeLimitHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.BlobHandlers,
                blobProcessHandlers);
        }
    }
    #endregion

    #region File type check handler configuration extenisions
    public static FileTypeCheckHandlerConfiguration GetFileTypeCheckConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new FileTypeCheckHandlerConfiguration(containerConfiguration);
    }

    public static void AddFileTypeCheckHandler(
        this BlobContainerConfiguration containerConfiguration,
        Action<FileTypeCheckHandlerConfiguration> configureAction)
    {
        var blobProcessHandlers = containerConfiguration.GetConfigurationOrDefault(
            BlobContainerConfigurationNames.BlobHandlers,
            new TypeList<IBlobHandler>());

        if (blobProcessHandlers.TryAdd<FileTypeCheckHandler>())
        {
            configureAction(new FileTypeCheckHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.BlobHandlers,
                blobProcessHandlers);
        }
    }
    #endregion

    #region Image resize handler configuration extenisions
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
            BlobContainerConfigurationNames.BlobHandlers,
            new TypeList<IBlobHandler>());

        if (blobProcessHandlers.TryAdd<ImageResizeHandler>())
        {
            configureAction(new ImageResizeHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.BlobHandlers,
                blobProcessHandlers);
        }
    }
    #endregion
}