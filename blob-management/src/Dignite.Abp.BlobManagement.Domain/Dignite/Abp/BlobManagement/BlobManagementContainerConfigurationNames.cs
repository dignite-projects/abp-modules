namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The keys <see cref="BlobManagementContainerConfiguration"/> keeps in a <c>BlobContainerConfiguration</c>.
/// </summary>
public static class BlobManagementContainerConfigurationNames
{
    public const string Managed = "Dignite.Abp.BlobManagement.Managed";

    public const string PublicRead = "Dignite.Abp.BlobManagement.PublicRead";

    public const string AllowedClientIds = "Dignite.Abp.BlobManagement.AllowedClientIds";

    public const string AllowDirectories = "Dignite.Abp.BlobManagement.AllowDirectories";
}
