using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Dignite.Abp.FileStoring;

public class ImageFormatHelper
{
    public static readonly IReadOnlyCollection<string> AllowedImageUploadFormats = new ReadOnlyCollection<string>(
        new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/bmp",
            "image/webp"
        });

    public static string AllowedImageFormatsJoint => string.Join(",", AllowedImageUploadFormats);

    public static bool IsValidImage(string mimeType, IEnumerable<string> validFormats)
    {
        try
        {
            return validFormats.Any(mime => mimeType.Equals(mime, System.StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
