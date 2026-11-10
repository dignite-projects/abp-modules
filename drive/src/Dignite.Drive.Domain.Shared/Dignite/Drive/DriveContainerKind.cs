namespace Dignite.Drive;

/// <summary>
/// Which entry point a Drive container serves. The two stay apart (see <c>drive/CLAUDE.md</c>).
/// </summary>
public enum DriveContainerKind
{
    /// <summary>
    /// Written through the BLOB storing provider (the integration entry): mapped from an application's container
    /// by <c>UseDrive</c>, not in the directory tree, not shown in the UI. Callers are clients and tickets, never
    /// users.
    /// </summary>
    System = 0,

    /// <summary>
    /// A user drive (the interaction entry): directories, the file picker and the Drive's own permissions.
    /// </summary>
    UserDrive = 1
}
