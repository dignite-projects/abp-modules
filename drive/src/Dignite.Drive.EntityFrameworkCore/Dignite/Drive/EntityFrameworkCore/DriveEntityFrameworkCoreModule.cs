using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Drive.EntityFrameworkCore;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class DriveEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<DriveDbContext>(options =>
        {
            options.AddDefaultRepositories<IDriveDbContext>(includeAllEntities: true);
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
