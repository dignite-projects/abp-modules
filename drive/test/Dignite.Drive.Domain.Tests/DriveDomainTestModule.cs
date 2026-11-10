using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Presets;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(AbpImagingSkiaSharpModule),
    typeof(DriveTestBaseModule)
)]
public class DriveDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        SingletonMemoryBlobProvider.Register(context.Services);

        // The Drive host's side: Drive containers on the in-memory provider, one with upload rules.
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(DriveTestContainers.Files, container =>
            {
                container.UseDriveContainer();
                container.UseMemory();
            });

            options.Containers.Configure(DriveTestContainers.Limited, container =>
            {
                container.UseDriveContainer();
                container.UseMemory();
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = DriveTestContainers.LimitedMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "application/pdf" });
            });

            options.Containers.Configure(DriveTestContainers.Images, container =>
            {
                container.UseDriveContainer();
                container.UseMemory();
                container.UseImagesPreset(preset =>
                {
                    preset.MaxSizeInMegabytes = 1;
                    preset.MaxWidth = DriveTestContainers.ImagesMaxWidth;
                    preset.MaxHeight = DriveTestContainers.ImagesMaxWidth;
                });
            });

            // Not a Drive container: the manager refuses it.
            options.Containers.Configure(DriveTestContainers.NotDrive, container =>
            {
                container.UseMemory();
            });
        });
    }
}
