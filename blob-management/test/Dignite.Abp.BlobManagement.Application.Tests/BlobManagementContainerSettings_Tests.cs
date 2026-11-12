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
using Volo.Abp.OpenIddict.Applications;
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
            ["BlobManagement:Containers:config-media:Kind"] = "System",
            ["BlobManagement:Containers:config-media:PublicRead"] = "true",
            ["BlobManagement:Containers:config-media:AllowedClientIds:0"] = "site-app",
            ["BlobManagement:Containers:config-media:AllowedClientIds:1"] = "ghost-app",
            ["BlobManagement:Containers:config-media:Preset"] = "images",
            ["BlobManagement:Containers:config-media:PresetOptions:MaxSizeInMegabytes"] = "2",
            ["BlobManagement:Containers:config-media:PresetOptions:MaxWidth"] = "100",
            ["BlobManagement:Containers:config-docs:Kind"] = "UserBlobManagement",
            ["BlobManagement:Containers:config-docs:Preset"] = "Documents"
        }));
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        // A host that can look clients up: it knows site-app, not ghost-app.
        services.AddSingleton<IApplicationFinder>(new FakeApplicationFinder("site-app", "site-app-staging"));
    }

    [Fact]
    public void Should_Declare_The_Configured_Containers_With_Their_Presets()
    {
        var provider = GetRequiredService<IBlobContainerConfigurationProvider>();

        var media = provider.Get(BlobManagementApplicationTestModule.ConfiguredAndOverriddenContainer);
        var mediaBlobManagement = media.GetBlobManagementContainerConfiguration();
        mediaBlobManagement.IsBlobManagementContainer.ShouldBeTrue();
        mediaBlobManagement.Kind.ShouldBe(BlobManagementContainerKind.System);
        mediaBlobManagement.AllowedClientIds.ShouldBe(new[] { "site-app", "ghost-app" });
        media.IsMultiTenant.ShouldBeTrue();
        media.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(2L * 1024 * 1024);
        media.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(100);
        media.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(BlobManagementPresetDefaults.ImagesMaxHeight);

        var docs = provider.Get(Docs);
        docs.GetBlobManagementContainerConfiguration().Kind.ShouldBe(BlobManagementContainerKind.UserBlobManagement);
        docs.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes.ShouldContain("application/pdf");
    }

    [Fact]
    public void Code_Should_Override_The_Configuration()
    {
        // PublicRead is true in configuration; BlobManagementApplicationTestModule sets it false in code.
        GetRequiredService<IBlobContainerConfigurationProvider>()
            .Get(BlobManagementApplicationTestModule.ConfiguredAndOverriddenContainer)
            .GetBlobManagementContainerConfiguration().PublicRead.ShouldBeFalse();
    }

    [Fact]
    public async Task The_Validator_Should_Warn_About_Unknown_Clients_And_Containers_No_Client_Can_Use()
    {
        var warnings = await GetRequiredService<BlobManagementContainerValidator>().ValidateAsync();

        warnings.ShouldContain(w => w.Contains("'ghost-app'"));
        warnings.ShouldNotContain(w => w.Contains("'site-app'"));
        // The test module's system containers allow no client.
        warnings.ShouldContain(w => w.Contains($"'{BlobManagementTestContainers.Files}'") && w.Contains("allows no client"));
        // A user drive is not a system container.
        warnings.ShouldNotContain(w => w.Contains($"'{Docs}'"));
    }

    [Fact]
    public async Task The_Validator_Should_Refuse_A_Default_Container_Declared_A_BlobManagement_Container()
    {
        var options = new AbpBlobStoringOptions();
        options.Containers.ConfigureDefault(container => container.UseBlobManagementContainer());

        await Should.ThrowAsync<AbpException>(() =>
            new BlobManagementContainerValidator(Options.Create(options), ServiceProvider).ValidateAsync());
    }

    [Theory]
    [InlineData("BlobManagement:Containers:c:Kidn", "System", "unknown key 'Kidn'")]
    [InlineData("BlobManagement:Containers:c:Kind", "Public", "is not a Kind")]
    [InlineData("BlobManagement:Containers:c:Scope", "Personal", "unknown key 'Scope'")]
    [InlineData("BlobManagement:Containers:c:PublicRead", "yes", "PublicRead must be true or false")]
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
    public void Reading_Should_Stop_At_Clients_On_A_User_BlobManagement()
    {
        var exception = Should.Throw<AbpException>(() => BlobManagementContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["BlobManagement:Containers:c:Kind"] = "UserBlobManagement",
            ["BlobManagement:Containers:c:AllowedClientIds:0"] = "site-app"
        }), new BlobManagementPresetOptions()));

        exception.Message.ShouldContain("system containers only");
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
        container.GetBlobManagementContainerConfiguration().Kind.ShouldBe(BlobManagementContainerKind.System);
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
