namespace Dignite.Abp.BlobManagement.Authorization;

/// <summary>
/// What a user may do with the directories of one container. Every directory in it shares the answer.
/// </summary>
public class BlobManagementAccess
{
    public static readonly BlobManagementAccess None = new(false, false, false, false);

    public BlobManagementAccess(
        bool view,
        bool createDirectories,
        bool updateDirectories,
        bool deleteDirectories)
    {
        View = view;
        CreateDirectories = view && createDirectories;
        UpdateDirectories = view && updateDirectories;
        DeleteDirectories = view && deleteDirectories;
    }

    /// <summary>See the container's directory tree. Nothing else is allowed without it.</summary>
    public bool View { get; }

    public bool CreateDirectories { get; }

    /// <summary>Rename and move directories.</summary>
    public bool UpdateDirectories { get; }

    public bool DeleteDirectories { get; }

    public BlobManagementAccessDto ToDto()
    {
        return new BlobManagementAccessDto
        {
            View = View,
            CreateDirectories = CreateDirectories,
            UpdateDirectories = UpdateDirectories,
            DeleteDirectories = DeleteDirectories
        };
    }
}
