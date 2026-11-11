namespace Dignite.Drive;

/// <summary>The containers <see cref="DriveDomainTestModule"/> configures.</summary>
public static class DriveTestContainers
{
    /// <summary>A system Drive container without upload rules.</summary>
    public const string Files = "files";

    /// <summary>A system Drive container that takes PDFs up to <see cref="LimitedMaxSizeInBytes"/>.</summary>
    public const string Limited = "limited";

    public const long LimitedMaxSizeInBytes = 1024;

    /// <summary>A system Drive container on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string Images = "images";

    public const int ImagesMaxWidth = 64;

    /// <summary>A system Drive container on the images preset with JPEGs stored as uploaded.</summary>
    public const string ImagesAsUploaded = "images-as-uploaded";

    /// <summary>A user drive on the images preset, scaling pictures into <see cref="ImagesMaxWidth"/> square.</summary>
    public const string UserImages = "user-images";

    /// <summary>A user drive.</summary>
    public const string UserDrive = "user-drive";

    /// <summary>A user drive whose files anyone may read.</summary>
    public const string PublicDrive = "public-drive";

    /// <summary>A BLOB container the Drive host did not declare a Drive container.</summary>
    public const string NotDrive = "not-drive";
}
