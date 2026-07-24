using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

public class FileTypeCheckHandler : IFileHandler, ITransientDependency
{
    public Task ExecuteAsync(FileHandlerContext context)
    {
        var fileTypeCheckHandlerConfiguration = context.ContainerConfiguration.GetFileTypeCheckConfiguration();

        if (fileTypeCheckHandlerConfiguration.AllowedFileTypeNames != null &&
            fileTypeCheckHandlerConfiguration.AllowedFileTypeNames.Length > 0)
        {
            var fileExtensionName = Path.GetExtension(context.FileName);

            if (!string.IsNullOrEmpty(fileExtensionName))
            {
                if (!fileTypeCheckHandlerConfiguration.AllowedFileTypeNames.Contains(fileExtensionName.ToLowerInvariant()))
                {
                    var allowedFileTypes = string.Join("/", fileTypeCheckHandlerConfiguration.AllowedFileTypeNames);
                    throw new BusinessException(
                        code: FileErrorCodes.Files.InvalidImageType,
                        message: "File type is incompatible! File type should be one of " + allowedFileTypes + "!",
                        details: "File type should be one of " + allowedFileTypes + "!"
                    );
                }
            }
            else
            {
                throw new BusinessException(
                    code: FileErrorCodes.Files.MissingFileExtension,
                    message: "File type is unrecognized!",
                    details: "Cannot get the file type of uploaded file!"
                );
            }
        }

        return Task.CompletedTask;
    }
}
