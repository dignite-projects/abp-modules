using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpDistributedLockingAbstractionsModule),
    typeof(AbpBlobStoringPipelineModule),
    typeof(AbpBlobStoringImagingModule),
    typeof(DriveDomainSharedModule)
)]
public class DriveDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // ABP's automatic distributed entity events, with its generic EntityEto: another service learns that a file
        // was created, changed or deleted without the Drive publishing hand-written ETOs. In one process
        // they go through the local bus.
        Configure<AbpDistributedEntityEventOptions>(options =>
        {
            options.AutoEventSelectors.Add<FileDescriptor>();
        });
    }
}
