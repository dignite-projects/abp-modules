using System.ComponentModel.DataAnnotations;

namespace Dignite.Drive.Public;

/// <summary>
/// The size a picture is read at. With both, it is cropped to fill the box; with one, the other follows its
/// proportions; with neither - and for anything that is not a JPEG, PNG or WebP - the file is served as stored.
/// </summary>
public class DrivePublicReadInput
{
    [Range(0, DrivePublicUrl.MaxDimension)]
    public int? Width { get; set; }

    [Range(0, DrivePublicUrl.MaxDimension)]
    public int? Height { get; set; }
}
