using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Abp.BlobManagement.Presets;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementDomainModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(AbpImagingSkiaSharpModule),
    typeof(BlobManagementTestBaseModule)
)]
public class BlobManagementDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        SingletonMemoryBlobProvider.Register(context.Services);

        // The Blob Management host's side: Blob Management containers on the in-memory provider, one with upload rules.
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(BlobManagementTestContainers.Files, container =>
            {
                container.UseBlobManagementContainer();
                container.UseMemory();
            });

            options.Containers.Configure(BlobManagementTestContainers.Limited, container =>
            {
                container.UseBlobManagementContainer();
                container.UseMemory();
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = BlobManagementTestContainers.LimitedMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "application/pdf" });
            });

            options.Containers.Configure(BlobManagementTestContainers.Images, container =>
            {
                container.UseBlobManagementContainer();
                container.UseMemory();
                container.UseImagesPreset(preset =>
                {
                    preset.MaxSizeInMegabytes = 1;
                    preset.MaxWidth = BlobManagementTestContainers.ImagesMaxWidth;
                    preset.MaxHeight = BlobManagementTestContainers.ImagesMaxWidth;
                });
            });

            options.Containers.Configure(BlobManagementTestContainers.ImagesAsUploaded, container =>
            {
                container.UseBlobManagementContainer();
                container.UseMemory();
                container.UseImagesPreset(preset =>
                {
                    preset.MaxSizeInMegabytes = 1;
                    preset.MaxWidth = BlobManagementTestContainers.ImagesMaxWidth;
                    preset.MaxHeight = BlobManagementTestContainers.ImagesMaxWidth;
                    preset.StoreJpegAsWebp = false;
                });
            });

            options.Containers.Configure(BlobManagementTestContainers.UserImages, container =>
            {
                container.UseBlobManagementContainer(drive => drive.Kind = BlobManagementContainerKind.UserBlobManagement);
                container.UseMemory();
                container.UseImagesPreset(preset =>
                {
                    preset.MaxSizeInMegabytes = 1;
                    preset.MaxWidth = BlobManagementTestContainers.ImagesMaxWidth;
                    preset.MaxHeight = BlobManagementTestContainers.ImagesMaxWidth;
                });
            });

            options.Containers.Configure(BlobManagementTestContainers.UserBlobManagement, container =>
            {
                container.UseBlobManagementContainer(drive => drive.Kind = BlobManagementContainerKind.UserBlobManagement);
                container.UseMemory();
            });

            options.Containers.Configure(BlobManagementTestContainers.PublicBlobManagement, container =>
            {
                container.UseBlobManagementContainer(drive =>
                {
                    drive.Kind = BlobManagementContainerKind.UserBlobManagement;
                    drive.PublicRead = true;
                });
                container.UseMemory();
            });

            // Not a Blob Management container: the manager refuses it.
            options.Containers.Configure(BlobManagementTestContainers.NotBlobManagement, container =>
            {
                container.UseMemory();
            });
        });
    }
}
