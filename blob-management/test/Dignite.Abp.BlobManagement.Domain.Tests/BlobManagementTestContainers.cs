namespace Dignite.Abp.BlobManagement;

/// <summary>The containers <see cref="BlobManagementDomainTestModule"/> configures.</summary>
public static class BlobManagementTestContainers
{
    /// <summary>A system Blob Management container without upload rules.</summary>
    public const string Files = "files";

    /// <summary>A system Blob Management container that takes PDFs up to <see cref="LimitedMaxSizeInBytes"/>.</summary>
    public const string Limited = "limited";

    public const long LimitedMaxSizeInBytes = 1024;

    /// <summary>A system Blob Management container on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string Images = "images";

    public const int ImagesMaxWidth = 64;

    /// <summary>A system Blob Management container on the images preset with JPEGs stored as uploaded.</summary>
    public const string ImagesAsUploaded = "images-as-uploaded";

    /// <summary>A user drive on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string UserImages = "user-images";

    /// <summary>A user drive.</summary>
    public const string UserBlobManagement = "user-drive";

    /// <summary>A user drive whose files anyone may read.</summary>
    public const string PublicBlobManagement = "public-drive";

    /// <summary>A BLOB container the Blob Management host did not declare a Blob Management container.</summary>
    public const string NotBlobManagement = "not-drive";
}
