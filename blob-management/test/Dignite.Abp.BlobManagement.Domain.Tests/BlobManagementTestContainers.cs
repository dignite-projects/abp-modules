namespace Dignite.Abp.BlobManagement;

/// <summary>The containers <see cref="BlobManagementDomainTestModule"/> configures.</summary>
public static class BlobManagementTestContainers
{
    /// <summary>A container without upload rules or directories.</summary>
    public const string Files = "files";

    /// <summary>A container that takes PDFs up to <see cref="LimitedMaxSizeInBytes"/>.</summary>
    public const string Limited = "limited";

    public const long LimitedMaxSizeInBytes = 1024;

    /// <summary>A container on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string Images = "images";

    public const int ImagesMaxWidth = 64;

    /// <summary>A container on the images preset with JPEGs stored as uploaded.</summary>
    public const string ImagesAsUploaded = "images-as-uploaded";

    /// <summary>Another container on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string UserImages = "user-images";

    /// <summary>A container that allows directories.</summary>
    public const string Documents = "documents";

    /// <summary>Another container that allows directories.</summary>
    public const string Archive = "archive";

    /// <summary>A BLOB container the Blob Management host did not declare.</summary>
    public const string NotManaged = "not-managed";
}
