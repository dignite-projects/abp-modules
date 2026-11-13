namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What the caller may do with the directories of one container. It is the container's answer: every directory in it
/// shares it.
/// </summary>
public class BlobManagementAccessDto
{
    /// <summary>See the container's directory tree. Nothing else is allowed without it.</summary>
    public bool View { get; set; }

    public bool CreateDirectories { get; set; }

    /// <summary>Rename and move directories.</summary>
    public bool UpdateDirectories { get; set; }

    /// <summary>Delete a directory, once it is empty.</summary>
    public bool DeleteDirectories { get; set; }
}
