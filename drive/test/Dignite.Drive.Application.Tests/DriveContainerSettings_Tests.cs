using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Containers;
using Dignite.Drive.Presets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.Applications;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// Drive containers declared in the <c>Drive</c> configuration section, as a host without code of its own declares
/// them, and the startup checks of <see cref="DriveContainerValidator"/>.
/// </summary>
public abstract class DriveContainerSettings_Tests<TStartupModule> : DriveApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Docs = "config-docs";

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        base.SetAbpApplicationCreationOptions(options);

        options.Services.ReplaceConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["Drive:Containers:config-media:Kind"] = "System",
            ["Drive:Containers:config-media:PublicRead"] = "true",
            ["Drive:Containers:config-media:AllowedClientIds:0"] = "site-app",
            ["Drive:Containers:config-media:AllowedClientIds:1"] = "ghost-app",
            ["Drive:Containers:config-media:Preset"] = "images",
            ["Drive:Containers:config-media:PresetOptions:MaxSizeInMegabytes"] = "2",
            ["Drive:Containers:config-media:PresetOptions:MaxWidth"] = "100",
            ["Drive:Containers:config-docs:Kind"] = "UserDrive",
            ["Drive:Containers:config-docs:Preset"] = "Documents"
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

        var media = provider.Get(DriveApplicationTestModule.ConfiguredAndOverriddenContainer);
        var mediaDrive = media.GetDriveContainerConfiguration();
        mediaDrive.IsDriveContainer.ShouldBeTrue();
        mediaDrive.Kind.ShouldBe(DriveContainerKind.System);
        mediaDrive.AllowedClientIds.ShouldBe(new[] { "site-app", "ghost-app" });
        media.IsMultiTenant.ShouldBeTrue();
        media.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(2L * 1024 * 1024);
        media.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(100);
        media.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(DrivePresetDefaults.ImagesMaxHeight);

        var docs = provider.Get(Docs);
        docs.GetDriveContainerConfiguration().Kind.ShouldBe(DriveContainerKind.UserDrive);
        docs.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes.ShouldContain("application/pdf");
    }

    [Fact]
    public void Code_Should_Override_The_Configuration()
    {
        // PublicRead is true in configuration; DriveApplicationTestModule sets it false in code.
        GetRequiredService<IBlobContainerConfigurationProvider>()
            .Get(DriveApplicationTestModule.ConfiguredAndOverriddenContainer)
            .GetDriveContainerConfiguration().PublicRead.ShouldBeFalse();
    }

    [Fact]
    public async Task The_Validator_Should_Warn_About_Unknown_Clients_And_Containers_No_Client_Can_Use()
    {
        var warnings = await GetRequiredService<DriveContainerValidator>().ValidateAsync();

        warnings.ShouldContain(w => w.Contains("'ghost-app'"));
        warnings.ShouldNotContain(w => w.Contains("'site-app'"));
        // The test module's system containers allow no client.
        warnings.ShouldContain(w => w.Contains($"'{DriveTestContainers.Files}'") && w.Contains("allows no client"));
        // A user drive is not a system container.
        warnings.ShouldNotContain(w => w.Contains($"'{Docs}'"));
    }

    [Fact]
    public async Task The_Validator_Should_Refuse_A_Default_Container_Declared_A_Drive_Container()
    {
        var options = new AbpBlobStoringOptions();
        options.Containers.ConfigureDefault(container => container.UseDriveContainer());

        await Should.ThrowAsync<AbpException>(() =>
            new DriveContainerValidator(Options.Create(options), ServiceProvider).ValidateAsync());
    }

    [Theory]
    [InlineData("Drive:Containers:c:Kidn", "System", "unknown key 'Kidn'")]
    [InlineData("Drive:Containers:c:Kind", "Public", "is not a Kind")]
    [InlineData("Drive:Containers:c:Scope", "Personal", "unknown key 'Scope'")]
    [InlineData("Drive:Containers:c:PublicRead", "yes", "PublicRead must be true or false")]
    [InlineData("Drive:Containers:c:Preset", "Videos", "unknown preset 'Videos'")]
    [InlineData("Drive:Containers:c:PresetOptions:MaxSizeInMegabytes", "2", "PresetOptions is given without a Preset")]
    public void Reading_Should_Stop_At_A_Mistake(string key, string value, string message)
    {
        var exception = Should.Throw<AbpException>(() =>
            DriveContainerSettings.Read(Configuration(new Dictionary<string, string?> { [key] = value }), new DrivePresetOptions()));

        exception.Message.ShouldContain(message);
        exception.Message.ShouldContain("'c'");
    }

    [Fact]
    public void Reading_Should_Stop_At_A_Misspelt_Or_Invalid_Preset_Parameter()
    {
        Should.Throw<AbpException>(() => DriveContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["Drive:Containers:c:Preset"] = "Images",
            ["Drive:Containers:c:PresetOptions:MaxWidht"] = "100"
        }), new DrivePresetOptions()));

        Should.Throw<AbpException>(() => DriveContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["Drive:Containers:c:Preset"] = "Documents",
            ["Drive:Containers:c:PresetOptions:MaxSizeInMegabytes"] = "0"
        }), new DrivePresetOptions()));
    }

    [Fact]
    public void Reading_Should_Stop_At_Clients_On_A_User_Drive()
    {
        var exception = Should.Throw<AbpException>(() => DriveContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["Drive:Containers:c:Kind"] = "UserDrive",
            ["Drive:Containers:c:AllowedClientIds:0"] = "site-app"
        }), new DrivePresetOptions()));

        exception.Message.ShouldContain("system containers only");
    }

    [Fact]
    public void A_Host_Can_Add_A_Preset()
    {
        var presets = new DrivePresetOptions();
        presets.Presets["Avatars"] = (container, parameters) => container.UseImagesPreset(o =>
        {
            o.MaxWidth = 256;
            o.MaxHeight = 256;
            DrivePresetOptions.Bind(parameters, o);
        });

        var settings = DriveContainerSettings.Read(Configuration(new Dictionary<string, string?>
        {
            ["Drive:Containers:avatars:Preset"] = "avatars",
            ["Drive:Containers:avatars:PresetOptions:MaxWidth"] = "128"
        }), presets).Single();

        var container = new BlobContainerConfiguration();
        settings.Apply(container);
        container.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(128);
        container.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(256);
        container.GetDriveContainerConfiguration().Kind.ShouldBe(DriveContainerKind.System);
    }

    [Fact]
    public void No_Section_Means_No_Containers()
    {
        DriveContainerSettings.Read(Configuration(new Dictionary<string, string?>()), new DrivePresetOptions()).ShouldBeEmpty();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
