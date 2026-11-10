namespace Dignite.Drive;

/// <summary>An application's own containers, configured by <see cref="DriveApplicationTestModule"/> with <c>UseDrive</c>.</summary>
public static class DriveTestAppContainers
{
    /// <summary>To the Drive's <see cref="DriveTestContainers.Files"/>.</summary>
    public const string Media = "app-media";

    /// <summary>To the Drive's <see cref="DriveTestContainers.Files"/> as well.</summary>
    public const string Documents = "app-documents";

    /// <summary>With a rule of the application's own (PNG only), to <see cref="DriveTestContainers.Files"/>.</summary>
    public const string PicturesOnly = "app-pictures-only";

    /// <summary>To the Drive's <see cref="DriveTestContainers.Images"/>, whose preset resizes.</summary>
    public const string Images = "app-images";

    /// <summary>Not multi-tenant, to <see cref="DriveTestContainers.Files"/>.</summary>
    public const string HostLevel = "app-host-level";
}
