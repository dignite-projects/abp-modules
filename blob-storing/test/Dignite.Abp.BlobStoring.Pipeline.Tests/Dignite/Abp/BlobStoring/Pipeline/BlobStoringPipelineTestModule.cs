using System.Collections.Generic;
using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.Pipeline;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(DigniteAbpBlobStoringPipelineModule)
)]
public class BlobStoringPipelineTestModule : AbpModule
{
    public const string MaxSizeContainer = "max-size";
    public const string ImagesContainer = "images";
    public const string ImageWildcardContainer = "image-wildcard";
    public const string UnidentifiedAllowedContainer = "unidentified-allowed";
    public const string WordDocumentsContainer = "word-documents";
    public const string PlainTextContainer = "plain-text";
    public const string GZipContainer = "gzip";
    public const string GZipEncryptedContainer = "gzip-encrypted";
    public const string GZipMaxSizeContainer = "gzip-max-size";
    public const string ComposedContainer = "composed";

    public const long MaxSizeInBytes = 1024;
    public const long ComposedMaxSizeInBytes = 64 * 1024;

    public const string WordDocumentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // MemoryBlobProvider is transient and keeps its store per instance; share one instance so the tests
        // can read what a container stored straight from the provider.
        context.Services.RemoveAll(descriptor =>
            descriptor.ServiceType == typeof(IBlobProvider) && descriptor.ImplementationType == typeof(MemoryBlobProvider));
        context.Services.Replace(ServiceDescriptor.Singleton<MemoryBlobProvider, MemoryBlobProvider>());
        context.Services.AddSingleton<IBlobProvider>(serviceProvider => serviceProvider.GetRequiredService<MemoryBlobProvider>());

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container => container.UseMemory());

            options.Containers.Configure(MaxSizeContainer, container =>
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = MaxSizeInBytes));

            options.Containers.Configure(ImagesContainer, container =>
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/png", "IMAGE/JPEG"]));

            options.Containers.Configure(ImageWildcardContainer, container =>
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]));

            options.Containers.Configure(UnidentifiedAllowedContainer, container =>
                container.AddAllowedContentTypesContributor(c =>
                {
                    c.AllowedContentTypes = ["image/png"];
                    c.AllowUnidentified = true;
                }));

            options.Containers.Configure(WordDocumentsContainer, container =>
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = [WordDocumentType]));

            options.Containers.Configure(PlainTextContainer, container =>
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["text/plain"]));

            options.Containers.Configure(GZipContainer, container =>
                container.AddGZipContributor());

            options.Containers.Configure(GZipEncryptedContainer, container =>
            {
                container.AddGZipContributor(c => c.CompressionLevel = CompressionLevel.SmallestSize);
                container.UseEncryption(passPhrase: "pipeline-tests-passphrase");
            });

            // MaxSize is added after GZip on purpose: GZip runs first and must enforce the limit itself.
            options.Containers.Configure(GZipMaxSizeContainer, container =>
            {
                container.AddGZipContributor();
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = MaxSizeInBytes);
            });

            options.Containers.Configure(ComposedContainer, container =>
            {
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = ComposedMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["text/plain", "image/*"]);
                container.AddGZipContributor();
            });
        });
    }
}
