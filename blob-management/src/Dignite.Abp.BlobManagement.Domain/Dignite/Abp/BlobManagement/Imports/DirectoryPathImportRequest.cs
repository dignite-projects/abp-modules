using Volo.Abp;

namespace Dignite.Abp.BlobManagement.Imports;

/// <summary>What <see cref="DirectoryPathImporter"/> is asked to import.</summary>
public class DirectoryPathImportRequest
{
    public DirectoryPathImportRequest(string containerName, string propertyName, bool dryRun = true)
    {
        ContainerName = Check.NotNullOrWhiteSpace(containerName, nameof(containerName));
        PropertyName = Check.NotNullOrWhiteSpace(propertyName, nameof(propertyName));
        DryRun = dryRun;
    }

    /// <summary>The container whose files are filed; it must allow directories.</summary>
    public string ContainerName { get; }

    /// <summary>
    /// The extra property of a file that holds its directory path, as whoever stored the file wrote it.
    /// Blob Management knows nothing of the writer: the operator names the property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Report what an import would do and change nothing. The default, so nothing is written unless asked for.
    /// </summary>
    public bool DryRun { get; }

    /// <summary>What separates the directories of a path. Default: <c>/</c>.</summary>
    public char Separator { get; set; } = '/';
}
