using System;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

public class FileSizeLimitHandlerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public FileSizeLimitHandlerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    /// <summary>
    /// Maximum file size in megabytes.
    /// </summary>
    public int MaxFileSize
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(FileSizeLimitHandlerConfigurationNames.MaxFileSize);
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum file size must be greater than zero.");
            }

            _containerConfiguration.SetConfiguration(FileSizeLimitHandlerConfigurationNames.MaxFileSize, value);
        }
    }

    public long MaxFileSizeInBytes => (long)MaxFileSize * 1024 * 1024;
}
