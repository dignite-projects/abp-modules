namespace Dignite.Abp.BlobManagement;

/// <summary>An application's own containers, configured by <see cref="BlobManagementApplicationTestModule"/> with <c>UseBlobManagement</c>.</summary>
public static class BlobManagementTestAppContainers
{
    /// <summary>To Blob Management's <see cref="BlobManagementTestContainers.Files"/>.</summary>
    public const string Media = "app-media";

    /// <summary>To Blob Management's <see cref="BlobManagementTestContainers.Files"/> as well.</summary>
    public const string Documents = "app-documents";

    /// <summary>With a rule of the application's own (PNG only), to <see cref="BlobManagementTestContainers.Files"/>.</summary>
    public const string PicturesOnly = "app-pictures-only";

    /// <summary>To Blob Management's <see cref="BlobManagementTestContainers.Images"/>, whose preset resizes.</summary>
    public const string Images = "app-images";

    /// <summary>Not multi-tenant, to <see cref="BlobManagementTestContainers.Files"/>.</summary>
    public const string HostLevel = "app-host-level";
}
