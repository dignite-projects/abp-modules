using System;
using System.Text.Json.Serialization;
using Dignite.Abp.FileStoring;
using JetBrains.Annotations;
using Volo.Abp.Validation;

namespace Dignite.FileExplorer.Files;

public class UpdateFileInput
{
    private string? _cellName;
    private Guid? _directoryId;

    [CanBeNull]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxCellNameLength))]
    public string? CellName
    {
        get => _cellName;
        set
        {
            _cellName = value;
            CellNameSpecified = true;
        }
    }

    /// <summary>
    /// True when the client sent CellName, including an explicit null to clear it.
    /// </summary>
    [JsonIgnore]
    public bool CellNameSpecified { get; private set; }

    /// <summary>
    /// Modify the directory of the file
    /// </summary>
    public Guid? DirectoryId
    {
        get => _directoryId;
        set
        {
            _directoryId = value;
            DirectoryIdSpecified = true;
        }
    }

    /// <summary>
    /// True when the client sent DirectoryId, including an explicit null to move to root.
    /// </summary>
    [JsonIgnore]
    public bool DirectoryIdSpecified { get; private set; }

    /// <summary>
    /// Modify the name of the file
    /// </summary>
    [CanBeNull]
    [DynamicStringLength(typeof(FileConsts), nameof(FileConsts.MaxNameLength))]
    public string? Name { get; set; }
}
