namespace Dignite.Drive.Presets;

/// <summary>The parameters of a preset that only caps the size: documents, binary assets, any.</summary>
public class DriveSizePresetOptions
{
    public DriveSizePresetOptions(long maxSizeInMegabytes)
    {
        MaxSizeInMegabytes = maxSizeInMegabytes;
    }

    /// <summary>The largest file the container takes, in megabytes. Must be greater than zero.</summary>
    public long MaxSizeInMegabytes { get; set; }
}
