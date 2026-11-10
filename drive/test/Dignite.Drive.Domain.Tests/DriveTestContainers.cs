namespace Dignite.Drive;

/// <summary>The containers <see cref="DriveDomainTestModule"/> configures.</summary>
public static class DriveTestContainers
{
    /// <summary>A system Drive container without upload rules.</summary>
    public const string Files = "files";

    /// <summary>A system Drive container that takes PDFs up to <see cref="LimitedMaxSizeInBytes"/>.</summary>
    public const string Limited = "limited";

    public const long LimitedMaxSizeInBytes = 1024;

    /// <summary>A BLOB container the Drive host did not declare a Drive container.</summary>
    public const string NotDrive = "not-drive";
}
