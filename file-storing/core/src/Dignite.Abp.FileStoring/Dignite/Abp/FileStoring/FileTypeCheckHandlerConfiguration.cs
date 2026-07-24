using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

public class FileTypeCheckHandlerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public FileTypeCheckHandlerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    public string[]? AllowedFileTypeNames
    {
        get => _containerConfiguration.GetConfigurationOrDefault<string[]>(FileTypeCheckHandlerConfigurationNames.AllowedFileTypeNames, null);
        set => _containerConfiguration.SetConfiguration(FileTypeCheckHandlerConfigurationNames.AllowedFileTypeNames, value);
    }
}
