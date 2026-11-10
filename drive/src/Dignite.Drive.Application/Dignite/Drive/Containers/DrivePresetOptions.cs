using System;
using System.Collections.Generic;
using Dignite.Drive.Presets;
using Microsoft.Extensions.Configuration;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive.Containers;

/// <summary>
/// The presets a container declared in appsettings can name (<c>Drive:Containers:{name}:Preset</c>). Each applies a
/// preset to the container, binding its parameters from the <c>PresetOptions</c> section it is given (which may be
/// empty). Read while <c>DriveApplicationModule</c> configures its services, so add a preset with ABP's
/// <c>PreConfigure&lt;DrivePresetOptions&gt;</c> in a module's <c>PreConfigureServices</c>.
/// </summary>
public class DrivePresetOptions
{
    public const string Images = "Images";

    public const string Documents = "Documents";

    public const string BinaryAssets = "BinaryAssets";

    public const string Any = "Any";

    /// <summary>The presets by name, compared case-insensitively.</summary>
    public Dictionary<string, Action<BlobContainerConfiguration, IConfiguration>> Presets { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DrivePresetOptions()
    {
        Presets[Images] = (container, parameters) => container.UseImagesPreset(o => Bind(parameters, o));
        Presets[Documents] = (container, parameters) => container.UseDocumentsPreset(o => Bind(parameters, o));
        Presets[BinaryAssets] = (container, parameters) => container.UseBinaryAssetsPreset(o => Bind(parameters, o));
        Presets[Any] = (container, parameters) => container.UseAnyPreset(o => Bind(parameters, o));
    }

    /// <summary>
    /// Binds <paramref name="parameters"/> into <paramref name="options"/>, failing on a key the options do not have,
    /// so a misspelt parameter stops the host instead of being ignored.
    /// </summary>
    public static void Bind(IConfiguration parameters, object options)
    {
        parameters.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
    }
}
