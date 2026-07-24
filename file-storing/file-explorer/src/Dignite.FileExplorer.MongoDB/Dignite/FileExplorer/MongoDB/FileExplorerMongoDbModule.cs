using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.FileExplorer.MongoDB;

[DependsOn(
    typeof(FileExplorerDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class FileExplorerMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<FileExplorerMongoDbContext>(options =>
        {
            options.AddRepository<FileDescriptor, MongoFileDescriptorRepository>();
            options.AddRepository<DirectoryDescriptor, MongoDirectoryDescriptorRepository>();
        });
    }
}
