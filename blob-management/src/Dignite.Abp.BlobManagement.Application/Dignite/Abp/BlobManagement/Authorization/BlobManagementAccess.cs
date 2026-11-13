namespace Dignite.Abp.BlobManagement.Authorization;

/// <summary>
/// What a user may do at one place - the top of a container, or a directory. Every file and directory of the place
/// shares it.
/// </summary>
public class BlobManagementAccess
{
    public static readonly BlobManagementAccess None = new(false, false, false, false, false, false, false);

    public BlobManagementAccess(
        bool view,
        bool uploadFiles,
        bool updateFiles,
        bool deleteFiles,
        bool createDirectories,
        bool updateDirectories,
        bool deleteDirectories)
    {
        View = view;
        UploadFiles = view && uploadFiles;
        UpdateFiles = view && updateFiles;
        DeleteFiles = view && deleteFiles;
        CreateDirectories = view && createDirectories;
        UpdateDirectories = view && updateDirectories;
        DeleteDirectories = view && deleteDirectories;
    }

    /// <summary>List the place's directories and files. Nothing else is allowed without it.</summary>
    public bool View { get; }

    public bool UploadFiles { get; }

    /// <summary>Rename and move the files here.</summary>
    public bool UpdateFiles { get; }

    /// <summary>Delete any file here, whoever uploaded it.</summary>
    public bool DeleteFiles { get; }

    public bool CreateDirectories { get; }

    /// <summary>Rename and move the directories here.</summary>
    public bool UpdateDirectories { get; }

    public bool DeleteDirectories { get; }

    public BlobManagementAccessDto ToDto()
    {
        return new BlobManagementAccessDto
        {
            View = View,
            UploadFiles = UploadFiles,
            UpdateFiles = UpdateFiles,
            DeleteFiles = DeleteFiles,
            CreateDirectories = CreateDirectories,
            UpdateDirectories = UpdateDirectories,
            DeleteDirectories = DeleteDirectories
        };
    }
}
