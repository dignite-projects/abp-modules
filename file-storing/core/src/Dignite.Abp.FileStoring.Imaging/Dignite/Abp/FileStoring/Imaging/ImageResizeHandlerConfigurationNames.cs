namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandlerConfigurationNames
{
    public const string ImageHeight = "ImageResizeHandler.ImageHeight";
    public const string ImageWidth = "ImageResizeHandler.ImageWidth";
    public const string ImageSizeMustBeLargerThanPreset = "ImageResizeHandler.ImageSizeCouldBeLessThanPreset";
    public const string MaxImageWidth = "ImageResizeHandler.MaxImageWidth";
    public const string MaxImageHeight = "ImageResizeHandler.MaxImageHeight";
    public const string MaxPixelCount = "ImageResizeHandler.MaxPixelCount";
    public const string MaxDecompressionRatio = "ImageResizeHandler.MaxDecompressionRatio";
    public const string DecodeTimeoutSeconds = "ImageResizeHandler.DecodeTimeoutSeconds";
}
