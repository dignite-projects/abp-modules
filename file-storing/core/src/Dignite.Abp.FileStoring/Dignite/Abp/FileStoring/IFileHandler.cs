using System.Threading.Tasks;

namespace Dignite.Abp.FileStoring;

public interface IFileHandler
{
    Task ExecuteAsync(FileHandlerContext context);
}
