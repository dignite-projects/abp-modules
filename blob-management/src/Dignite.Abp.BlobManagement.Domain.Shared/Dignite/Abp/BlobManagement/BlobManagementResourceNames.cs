namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The resource name of Blob Management's resource permissions: the full name of the class they are granted on, as
/// ABP's <c>KeyedObjectResourcePermissionRequirementHandler</c> derives it. The class lives in the Domain layer, out
/// of this assembly's reach, so the name is spelt out here. Renaming that class would orphan every grant stored under
/// its name: it is a contract from the first release.
/// </summary>
public static class BlobManagementResourceNames
{
    /// <summary><c>Dignite.Abp.BlobManagement.BlobManagementContainer</c>, keyed by the container name.</summary>
    public const string Container = "Dignite.Abp.BlobManagement.BlobManagementContainer";
}
