namespace Dignite.FileExplorer;

public static class FileExplorerErrorCodes
{
    public static class Directories
    {
        public const string DirectoryAlreadyExist = "Dignite.FileExplorer:Directory:0001";
        public const string DirectoryNotExist = "Dignite.FileExplorer:Directory:0002";
        public const string InvalidDirectoryName = "Dignite.FileExplorer:Directory:0003";
        public const string InvalidMove = "Dignite.FileExplorer:Directory:0004";
        public const string ForbidMovingToChild = "Dignite.FileExplorer:Directory:0005";
        public const string DirectoryNotEmpty = "Dignite.FileExplorer:Directory:0006";
    }
    public static class Files
    {
        public const string CellNameNotApplicable = "Dignite.FileExplorer:File:0001";
        public const string CellNameNotFound = "Dignite.FileExplorer:File:0002";
        public const string ImageSizeTooSmall = "Dignite.FileExplorer:File:0003";
        public const string ImageResizeFailure = "Dignite.FileExplorer:File:0004";
        public const string InvalidSorting = "Dignite.FileExplorer:File:0005";
        //public const string ImageCompressionFailure = "Dignite.FileExplorer:File:0006";
    }
}
