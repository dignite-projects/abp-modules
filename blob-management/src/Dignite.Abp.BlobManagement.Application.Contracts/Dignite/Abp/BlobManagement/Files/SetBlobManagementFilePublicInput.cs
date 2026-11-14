namespace Dignite.Abp.BlobManagement.Files;

public class SetBlobManagementFilePublicInput
{
    /// <summary>Whether anyone may read the file at its public address, without signing in.</summary>
    public bool IsPublic { get; set; }
}
