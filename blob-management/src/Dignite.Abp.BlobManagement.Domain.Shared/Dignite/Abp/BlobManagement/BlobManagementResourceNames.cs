namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The resource names of Blob Management's resource permissions: the full names of the classes they are granted on, as
/// ABP's <c>KeyedObjectResourcePermissionRequirementHandler</c> derives them. The classes live in the Domain layer, out
/// of this assembly's reach, so the names are spelt out here. Renaming one of those classes would orphan every grant
/// stored under its name: they are contracts from the first release.
/// </summary>
public static class BlobManagementResourceNames
{
    /// <summary><c>Dignite.Abp.BlobManagement.BlobManagementContainer</c>, keyed by the container name.</summary>
    public const string Container = "Dignite.Abp.BlobManagement.BlobManagementContainer";

    /// <summary><c>Dignite.Abp.BlobManagement.DirectoryDescriptor</c>, keyed by the directory id.</summary>
    public const string Directory = "Dignite.Abp.BlobManagement.DirectoryDescriptor";
}
