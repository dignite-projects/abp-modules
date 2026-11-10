using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class DriveMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<DriveMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<IDriveMongoDbContext>();
            
            /* Add custom repositories here. Example:
             * options.AddRepository<Question, MongoQuestionRepository>();
             */
        });
    }
}
