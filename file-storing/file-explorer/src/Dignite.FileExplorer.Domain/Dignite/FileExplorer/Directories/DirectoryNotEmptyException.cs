using Volo.Abp;

namespace Dignite.FileExplorer.Directories;

public class DirectoryNotEmptyException : BusinessException
{
    public DirectoryNotEmptyException()
    {
        Code = FileExplorerErrorCodes.Directories.DirectoryNotEmpty;
    }
}
