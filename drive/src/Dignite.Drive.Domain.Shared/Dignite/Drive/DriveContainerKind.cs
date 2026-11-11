namespace Dignite.Drive;

/// <summary>
/// Which entry point a Drive container serves. The two stay apart (see <c>drive/CLAUDE.md</c>).
/// </summary>
public enum DriveContainerKind
{
    /// <summary>
    /// Written through the BLOB storing provider (the integration entry): mapped from an application's container
    /// by <c>UseDrive</c>, addressed by the application's own names, not shown to users. Callers are clients and
    /// tickets, never users.
    /// </summary>
    System = 0,

    /// <summary>
    /// A user drive (the interaction entry): signed-in users upload files to it and read or delete them by id, under the
    /// Drive's own permissions.
    /// </summary>
    UserDrive = 1
}
