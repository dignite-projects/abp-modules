namespace Dignite.Abp.BlobManagement.Presets;

/// <summary>The parameters of a preset that only caps the size: documents, binary assets, any.</summary>
public class BlobManagementSizePresetOptions
{
    public BlobManagementSizePresetOptions(long maxSizeInMegabytes)
    {
        MaxSizeInMegabytes = maxSizeInMegabytes;
    }

    /// <summary>The largest file the container takes, in megabytes. Must be greater than zero.</summary>
    public long MaxSizeInMegabytes { get; set; }
}
