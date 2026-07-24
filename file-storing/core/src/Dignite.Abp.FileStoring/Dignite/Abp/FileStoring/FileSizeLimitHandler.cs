using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

public class FileSizeLimitHandler : IFileHandler, ITransientDependency
{
    public Task ExecuteAsync(FileHandlerContext context)
    {
        var configuration = context.ContainerConfiguration.GetFileSizeLimitConfiguration();
        if (configuration.MaxFileSize <= 0)
        {
            throw new InvalidOperationException("A positive maximum file size in megabytes must be configured.");
        }

        if (configuration.MaxFileSizeInBytes < context.BlobStream.Length)
        {
            throw new BusinessException(
                code: FileErrorCodes.Files.FileTooLarge,
                message: "File object is too large",
                details: $"The file object size cannot exceed {configuration.MaxFileSize} MB!"
            );
        }

        return Task.CompletedTask;
    }
}
