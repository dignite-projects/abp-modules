using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BlobStoring;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementDomainModule),
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class BlobManagementApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<BlobManagementApplicationModule>();

        // Upload tickets and read links are signed with the host's key ring; the host decides where it is kept.
        context.Services.AddDataProtection();

        ConfigureContainersFromConfiguration(context);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<BlobManagementContainerValidator>().Validate();
    }

    /// <summary>
    /// Declares the containers of the <c>Blob Management</c> configuration section (see <see cref="BlobManagementContainerSettings"/>).
    /// They are read and checked now, so a mistake stops the host; they are applied in this module's place in ABP's
    /// order, so a dependent module's or the host's own <c>Configure&lt;AbpBlobStoringOptions&gt;</c> - which runs later -
    /// overrides them.
    /// </summary>
    private void ConfigureContainersFromConfiguration(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var presets = context.Services.ExecutePreConfiguredActions<BlobManagementPresetOptions>();
        var containers = BlobManagementContainerSettings.Read(configuration, presets);
        var defaultPublicSizes = BlobManagementContainerSettings.ReadDefaultPublicSizes(configuration);
        if (containers.Count == 0 && defaultPublicSizes == null)
        {
            return;
        }

        Configure<AbpBlobStoringOptions>(options =>
        {
            if (defaultPublicSizes != null)
            {
                options.Containers.ConfigureDefault(container => container.UseBlobManagementPublicSizes(defaultPublicSizes));
            }

            foreach (var container in containers)
            {
                options.Containers.Configure(container.ContainerName, container.Apply);
            }
        });
    }
}
