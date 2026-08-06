namespace Dignite.Abp.FlexFields.CKEditor;

public class CKEditorConfiguration : FieldConfigurationBase
{
    public CKEditorMode Mode {
        get => ConfigurationDictionary.GetConfiguration(CKEditorConfigurationNames.Mode, CKEditorMode.Full);
        set => ConfigurationDictionary.SetConfiguration(CKEditorConfigurationNames.Mode, value);
    }

    public CKEditorContentFormat ContentFormat {
        get => ConfigurationDictionary.GetConfiguration(CKEditorConfigurationNames.ContentFormat, CKEditorContentFormat.Html);
        set => ConfigurationDictionary.SetConfiguration(CKEditorConfigurationNames.ContentFormat, value);
    }

    /// <summary>
    /// Blob container the image-upload adapter posts to. No default - unset simply means the Angular
    /// control omits the image-upload toolbar button entirely, rather than the FileExplorer field
    /// type's harder failure mode of the whole control being unusable, since a CKEditor field is still
    /// perfectly usable with no image support at all.
    /// </summary>
    public string? ImagesContainerName {
        get => ConfigurationDictionary.GetConfiguration<string?>(CKEditorConfigurationNames.ImagesContainerName, null);
        set => ConfigurationDictionary.SetConfiguration(CKEditorConfigurationNames.ImagesContainerName, value);
    }

    public string InitialContent {
        get => ConfigurationDictionary.GetConfiguration(CKEditorConfigurationNames.InitialContent, string.Empty);
        set => ConfigurationDictionary.SetConfiguration(CKEditorConfigurationNames.InitialContent, value);
    }

    public CKEditorConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public CKEditorConfiguration() : base()
    {
    }
}
