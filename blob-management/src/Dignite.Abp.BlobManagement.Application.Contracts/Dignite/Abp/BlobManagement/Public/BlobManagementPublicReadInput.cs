using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Public;

/// <summary>
/// The size a picture is read at. With both, it is cropped to fill the box; with one, the other follows its
/// proportions; with neither - and for anything that is not a JPEG, PNG or WebP - the file is served as stored.
/// </summary>
public class BlobManagementPublicReadInput
{
    [Range(0, BlobManagementPublicUrl.MaxDimension)]
    public int? Width { get; set; }

    [Range(0, BlobManagementPublicUrl.MaxDimension)]
    public int? Height { get; set; }
}
