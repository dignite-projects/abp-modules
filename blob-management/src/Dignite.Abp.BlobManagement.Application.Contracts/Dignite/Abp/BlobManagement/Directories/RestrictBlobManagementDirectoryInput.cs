namespace Dignite.Abp.BlobManagement.Directories;

public class RestrictBlobManagementDirectoryInput
{
    /// <summary>Whether the directory has permissions of its own.</summary>
    public bool IsRestricted { get; set; }
}
