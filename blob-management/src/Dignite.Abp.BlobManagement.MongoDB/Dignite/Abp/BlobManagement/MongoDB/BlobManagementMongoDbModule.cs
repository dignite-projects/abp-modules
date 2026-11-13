using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

[DependsOn(
    typeof(BlobManagementDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class BlobManagementMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<BlobManagementMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<IBlobManagementMongoDbContext>();

            options.AddRepository<FileDescriptor, MongoFileDescriptorRepository>();
            options.AddRepository<DirectoryDescriptor, MongoDirectoryDescriptorRepository>();
        });
    }
}
