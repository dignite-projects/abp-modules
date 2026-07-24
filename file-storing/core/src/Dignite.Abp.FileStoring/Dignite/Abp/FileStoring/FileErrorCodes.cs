namespace Dignite.Abp.FileStoring;

public static class FileErrorCodes
{
    public static class Files
    {
        public const string FileTooLarge = "Dignite.Abp.File:0001";
        public const string InvalidImageType = "Dignite.Abp.File:0002";
        public const string MissingFileExtension = "Dignite.Abp.File:0003";
    }

    public static class Containers
    {
        public const string NotFound = "Dignite.Abp.File:0004";
    }
}
