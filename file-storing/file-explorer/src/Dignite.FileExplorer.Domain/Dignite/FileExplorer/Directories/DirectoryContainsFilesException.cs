using Volo.Abp;

namespace Dignite.FileExplorer.Directories;

public class DirectoryContainsFilesException : BusinessException
{
    public DirectoryContainsFilesException()
    {
        Code = FileExplorerErrorCodes.Directories.DirectoryContainsFiles;
    }
}
