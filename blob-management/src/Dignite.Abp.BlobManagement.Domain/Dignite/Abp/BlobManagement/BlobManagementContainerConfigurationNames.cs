namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The keys <see cref="BlobManagementContainerConfiguration"/> keeps in a <c>BlobContainerConfiguration</c>.
/// </summary>
public static class BlobManagementContainerConfigurationNames
{
    public const string Managed = "Dignite.Abp.BlobManagement.Managed";

    public const string AllowDirectories = "Dignite.Abp.BlobManagement.AllowDirectories";

    /// <summary>The sizes a public read resizes to; set on a container, or on the default container for every one.</summary>
    public const string PublicSizes = "Dignite.Abp.BlobManagement.PublicSizes";
}
