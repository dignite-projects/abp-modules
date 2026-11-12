using System;
using System.Collections.Generic;

namespace Dignite.Abp.BlobManagement.Imports;

/// <summary>
/// What a directory import did, or - for a dry run - would do. Run again, an import reports the files it filed before as
/// already filed and creates nothing it created before.
/// </summary>
public class DirectoryPathImportReport
{
    public DirectoryPathImportReport(string containerName, string propertyName, bool dryRun)
    {
        ContainerName = containerName;
        PropertyName = propertyName;
        DryRun = dryRun;
    }

    public string ContainerName { get; }

    public string PropertyName { get; }

    /// <summary>Whether this is a report of what would be done, with nothing changed.</summary>
    public bool DryRun { get; }

    /// <summary>How many unfiled files of the container were read.</summary>
    public int FilesRead { get; set; }

    /// <summary>Unfiled files without a path in the property: left at the top of their container.</summary>
    public int FilesWithoutPath { get; set; }

    /// <summary>The files filed into a directory (would be, in a dry run).</summary>
    public List<FiledFile> FilesFiled { get; } = new();

    /// <summary>The directories created (would be, in a dry run), each as its path, from the top of the container.</summary>
    public List<string> DirectoriesCreated { get; } = new();

    /// <summary>The directories that existed already and were used as they are, case aside.</summary>
    public List<string> DirectoriesReused { get; } = new();

    /// <summary>The files with a path that could not be filed, and why. They stay where they are.</summary>
    public List<SkippedFile> FilesSkipped { get; } = new();

    public class FiledFile
    {
        public FiledFile(Guid fileId, string path)
        {
            FileId = fileId;
            Path = path;
        }

        public Guid FileId { get; }

        /// <summary>The path the file was filed under, as normalized.</summary>
        public string Path { get; }
    }

    public class SkippedFile
    {
        public SkippedFile(Guid fileId, string path, string reason)
        {
            FileId = fileId;
            Path = path;
            Reason = reason;
        }

        public Guid FileId { get; }

        /// <summary>The path as the property held it.</summary>
        public string Path { get; }

        public string Reason { get; }
    }
}
