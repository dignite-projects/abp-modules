using System;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;

namespace Dignite.Abp.FileStoring;

public static class BlobContainerConfigurationExtensions
{
    public static FileSizeLimitHandlerConfiguration GetFileSizeLimitConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new FileSizeLimitHandlerConfiguration(containerConfiguration);
    }

    public static void AddFileSizeLimitHandler(
        this BlobContainerConfiguration containerConfiguration,
        Action<FileSizeLimitHandlerConfiguration> configureAction)
    {
        var fileProcessHandlers = containerConfiguration.GetConfigurationOrDefault(
            BlobContainerConfigurationNames.FileHandlers,
            new TypeList<IFileHandler>())!;

        if (fileProcessHandlers.TryAdd<FileSizeLimitHandler>())
        {
            configureAction(new FileSizeLimitHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.FileHandlers,
                fileProcessHandlers);
        }
    }

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
            BlobContainerConfigurationNames.FileHandlers,
            new TypeList<IFileHandler>())!;

        if (blobProcessHandlers.TryAdd<FileTypeCheckHandler>())
        {
            configureAction(new FileTypeCheckHandlerConfiguration(containerConfiguration));

            containerConfiguration.SetConfiguration(
                BlobContainerConfigurationNames.FileHandlers,
                blobProcessHandlers);
        }
    }
}
