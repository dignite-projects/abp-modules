using System.Threading.Tasks;
using Dignite.Drive.Containers;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BlobStoring;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(DriveApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class DriveApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<DriveApplicationModule>();

        ConfigureContainersFromConfiguration(context);
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.ServiceProvider.GetRequiredService<DriveContainerValidator>().ValidateAsync();
    }

    /// <summary>
    /// Declares the containers of the <c>Drive</c> configuration section (see <see cref="DriveContainerSettings"/>).
    /// They are read and checked now, so a mistake stops the host; they are applied in this module's place in ABP's
    /// order, so a dependent module's or the host's own <c>Configure&lt;AbpBlobStoringOptions&gt;</c> - which runs later -
    /// overrides them.
    /// </summary>
    private void ConfigureContainersFromConfiguration(ServiceConfigurationContext context)
    {
        var presets = context.Services.ExecutePreConfiguredActions<DrivePresetOptions>();
        var containers = DriveContainerSettings.Read(context.Services.GetConfiguration(), presets);
        if (containers.Count == 0)
        {
            return;
        }

        Configure<AbpBlobStoringOptions>(options =>
        {
            foreach (var container in containers)
            {
                options.Containers.Configure(container.ContainerName, container.Apply);
            }
        });
    }
}
