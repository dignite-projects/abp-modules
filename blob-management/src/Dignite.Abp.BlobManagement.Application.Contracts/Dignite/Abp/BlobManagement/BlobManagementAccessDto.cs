namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What the caller may do at one place - the top of a container, or a directory. Every file and every directory of the
/// place shares the answer: it is worked out once for the place, not for each item.
/// </summary>
public class BlobManagementAccessDto
{
    /// <summary>List the place's directories and files.</summary>
    public bool View { get; set; }

    public bool UploadFiles { get; set; }

    /// <summary>Rename and move the files here.</summary>
    public bool UpdateFiles { get; set; }

    /// <summary>Delete any file here. Without it, the caller still deletes a file they uploaded.</summary>
    public bool DeleteFiles { get; set; }

    public bool CreateDirectories { get; set; }

    /// <summary>Rename and move the directories here.</summary>
    public bool UpdateDirectories { get; set; }

    public bool DeleteDirectories { get; set; }
}
