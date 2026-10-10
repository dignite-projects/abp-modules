using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FileStoring;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(DigniteAbpFileStoringModule)
)]
public class FileStoringTestModule : AbpModule
{
    public const string DocumentsContainer = "documents";
    public const string PipelineContainer = "pipeline";
    public const string FixedNameContainer = "fixed-name";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container => container.ProviderType = typeof(FakeBlobProvider));

            options.Containers.Configure(DocumentsContainer, container =>
            {
                container.ProviderType = typeof(FakeBlobProvider);
                container.AddFileSizeLimitHandler(handler => handler.MaxFileSize = 1);
            });

            options.Containers.Configure(PipelineContainer, container =>
            {
                container.ProviderType = typeof(FakeBlobProvider);
                var handlers = new TypeList<IFileHandler>();
                handlers.Add<RecordingHandler>();
                handlers.Add<UppercaseHandler>();
                handlers.Add<SecondRecordingHandler>();
                container.SetConfiguration(BlobContainerConfigurationNames.FileHandlers, handlers);
            });

            options.Containers.Configure(FixedNameContainer, container =>
            {
                container.ProviderType = typeof(FakeBlobProvider);
                container.SetBlobNameGenerator<FixedBlobNameGenerator>();
            });
        });
    }
}
