namespace Dignite.Abp.FlexFields.FileExplorer;

public class FileExplorerConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// Blob container the picker browses/uploads to. No default on this side either - see the doc
    /// comment on <see cref="FileExplorerFieldType"/> for why an unset value is left for the config
    /// editor to reject rather than silently substituted.
    /// </summary>
    public string? FileContainerName {
        get => ConfigurationDictionary.GetConfiguration<string?>(FileExplorerConfigurationNames.FileContainerName, null);
        set => ConfigurationDictionary.SetConfiguration(FileExplorerConfigurationNames.FileContainerName, value);
    }

    public bool UploadFileMultiple {
        get => ConfigurationDictionary.GetConfiguration(FileExplorerConfigurationNames.UploadFileMultiple, false);
        set => ConfigurationDictionary.SetConfiguration(FileExplorerConfigurationNames.UploadFileMultiple, value);
    }

    public FileExplorerConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public FileExplorerConfiguration() : base()
    {
    }
}
