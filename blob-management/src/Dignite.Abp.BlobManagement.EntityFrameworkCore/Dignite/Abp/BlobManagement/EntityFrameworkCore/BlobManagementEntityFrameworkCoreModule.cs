using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

[DependsOn(
    typeof(BlobManagementDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class BlobManagementEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<BlobManagementDbContext>(options =>
        {
            options.AddDefaultRepositories<IBlobManagementDbContext>(includeAllEntities: true);

            options.AddRepository<FileDescriptor, EfCoreFileDescriptorRepository>();
            options.AddRepository<DirectoryDescriptor, EfCoreDirectoryDescriptorRepository>();
        });
    }
}
