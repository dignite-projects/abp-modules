using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Presets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Blob Management containers declared in the <c>Blob Management</c> configuration section, as a host without code of its own declares
/// them, and the startup checks of <see cref="BlobManagementContainerValidator"/>.
/// </summary>
public abstract class BlobManagementContainerSettings_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Docs = "config-docs";

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        base.SetAbpApplicationCreationOptions(options);

        options.Services.ReplaceConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:config-media:AllowDirectories"] = "true",
            ["BlobManagement:Containers:config-media:Preset"] = "images",
            ["BlobManagement:Containers:config-media:PresetOptions:MaxSizeInMegabytes"] = "2",
            ["BlobManagement:Containers:config-media:PresetOptions:MaxWidth"] = "100",
            ["BlobManagement:Containers:config-docs:AllowDirectories"] = "true",
            ["BlobManagement:Containers:config-docs:Preset"] = "Documents"
        }));
    }

    [Fact]
    public void Should_Declare_The_Configured_Containers_With_Their_Presets()
    {
        var provider = GetRequiredService<IBlobContainerConfigurationProvider>();

        var media = provider.Get(BlobManagementApplicationTestModule.ConfiguredAndOverriddenContainer);
        var mediaBlobManagement = media.GetBlobManagementContainerConfiguration();
        mediaBlobManagement.IsManaged.ShouldBeTrue();
        mediaBlobManagement.AllowDirectories.ShouldBeFalse();
        media.IsMultiTenant.ShouldBeTrue();
        media.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(2L * 1024 * 1024);
        media.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(100);
        media.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(BlobManagementPresetDefaults.ImagesMaxHeight);

        var docs = provider.Get(Docs);
        docs.GetBlobManagementContainerConfiguration().AllowDirectories.ShouldBeTrue();
        docs.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes.ShouldContain("application/pdf");
    }

    [Fact]
    public void Code_Should_Override_The_Configuration()
    {
        // AllowDirectories is true in configuration; BlobManagementApplicationTestModule sets it false in code.
        GetRequiredService<IBlobContainerConfigurationProvider>()
            .Get(BlobManagementApplicationTestModule.ConfiguredAndOverriddenContainer)
            .GetBlobManagementContainerConfiguration().AllowDirectories.ShouldBeFalse();
    }

    [Fact]
    public void The_Validator_Should_Refuse_A_Default_Container_Declared_A_BlobManagement_Container()
    {
        var options = new AbpBlobStoringOptions();
        options.Containers.ConfigureDefault(container => container.UseBlobManagementContainer());

        Should.Throw<AbpException>(() => new BlobManagementContainerValidator(Options.Create(options)).Validate());
    }

    [Theory]
    [InlineData("BlobManagement:Containers:c:Kidn", "System", "unknown key 'Kidn'")]
    [InlineData("BlobManagement:Containers:c:Scope", "Personal", "unknown key 'Scope'")]
    // A client is granted on a container in ABP's permission management now, not listed in its configuration.
    [InlineData("BlobManagement:Containers:c:AllowedClientIds:0", "site-app", "unknown key 'AllowedClientIds'")]
    // Whether a file is read publicly is its business system's call, made on the file, not the container's.
    [InlineData("BlobManagement:Containers:c:PublicRead", "true", "unknown key 'PublicRead'")]
    [InlineData("BlobManagement:Containers:c:Preset", "Videos", "unknown preset 'Videos'")]
    [InlineData("BlobManagement:Containers:c:PresetOptions:MaxSizeInMegabytes", "2", "PresetOptions is given without a Preset")]
    public void Reading_Should_Stop_At_A_Mistake(string key, string value, string message)
    {
        var exception = Should.Throw<AbpException>(() =>
            BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?> { [key] = value }), new BlobManagementPresetOptions()));

        exception.Message.ShouldContain(message);
        exception.Message.ShouldContain("'c'");
    }

    [Fact]
    public void Reading_Should_Stop_At_A_Misspelt_Or_Invalid_Preset_Parameter()
    {
        Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:c:Preset"] = "Images",
            ["BlobManagement:Containers:c:PresetOptions:MaxWidht"] = "100"
        }), new BlobManagementPresetOptions()));

        Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:c:Preset"] = "Documents",
            ["BlobManagement:Containers:c:PresetOptions:MaxSizeInMegabytes"] = "0"
        }), new BlobManagementPresetOptions()));
    }

    [Fact]
    public void Reading_Should_Stop_At_A_Value_That_Is_Not_A_Boolean_And_At_The_Removed_Kind()
    {
        Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:c:AllowDirectories"] = "yes"
        }), new BlobManagementPresetOptions())).Message.ShouldContain("AllowDirectories");

        // There is one kind of container: the key is unknown now, and says so at startup.
        Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:c:Kind"] = "System"
        }), new BlobManagementPresetOptions())).Message.ShouldContain("unknown key 'Kind'");
    }

    [Fact]
    public void A_Host_Can_Add_A_Preset()
    {
        var presets = new BlobManagementPresetOptions();
        presets.Presets["Avatars"] = (container, parameters) => container.UseImagesPreset(o =>
        {
            o.MaxWidth = 256;
            o.MaxHeight = 256;
            BlobManagementPresetOptions.Bind(parameters, o);
        });

        var settings = BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:avatars:Preset"] = "avatars",
            ["BlobManagement:Containers:avatars:PresetOptions:MaxWidth"] = "128"
        }), presets).Single();

        var container = new BlobContainerConfiguration();
        settings.Apply(container);
        container.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(128);
        container.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(256);
        container.GetBlobManagementContainerConfiguration().IsManaged.ShouldBeTrue();
    }

    [Fact]
    public void The_Images_Preset_Should_Store_Jpegs_As_Webp_Unless_Configuration_Turns_It_Off()
    {
        var settings = BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:photos:Preset"] = "Images",
            ["BlobManagement:Containers:photos:PresetOptions:WebpQuality"] = "70",
            ["BlobManagement:Containers:originals:Preset"] = "Images",
            ["BlobManagement:Containers:originals:PresetOptions:StoreJpegAsWebp"] = "false"
        }), new BlobManagementPresetOptions());

        var photos = new BlobContainerConfiguration();
        settings.Single(s => s.ContainerName == "photos").Apply(photos);
        photos.GetStoredContentType("image/jpeg").ShouldBe("image/webp");
        photos.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(70);

        var originals = new BlobContainerConfiguration();
        settings.Single(s => s.ContainerName == "originals").Apply(originals);
        originals.GetStoredContentType("image/jpeg").ShouldBe("image/jpeg");
        originals.PipelineContributors.ShouldNotContain(typeof(JpegToWebpContributor));
    }

    [Fact]
    public void A_Webp_Quality_Out_Of_Range_Should_Stop_The_Host()
    {
        Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:photos:Preset"] = "Images",
            ["BlobManagement:Containers:photos:PresetOptions:WebpQuality"] = "0"
        }), new BlobManagementPresetOptions()));
    }

    [Fact]
    public void No_Section_Means_No_Containers()
    {
        BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>()), new BlobManagementPresetOptions()).ShouldBeEmpty();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
