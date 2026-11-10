using Dignite.Abp.AspNetCore.Mcp;
using Volo.Abp.Modularity;

namespace Dignite.Drive.Mcp;

[DependsOn(
    typeof(DriveApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
    )]
public class DriveMcpModule : AbpModule
{
}
