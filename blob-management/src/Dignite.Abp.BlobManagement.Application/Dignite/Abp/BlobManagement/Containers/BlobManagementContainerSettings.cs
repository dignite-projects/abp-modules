using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// Blob Management containers declared in configuration, for a host that has no code of its own to declare them (the Blob Management run
/// as its own service). The root-level section:
/// <code>
/// "BlobManagement": {
///   "Containers": {
///     "media": {
///       "Kind": "System",
///       "PublicRead": true,
///       "AllowedClientIds": [ "site-app" ],
///       "Preset": "Images",
///       "PresetOptions": { "MaxSizeInMegabytes": 10, "MaxWidth": 2048 }
///     },
///     "documents": {
///       "Kind": "UserBlobManagement",
///       "Preset": "Documents"
///     }
///   }
/// }
/// </code>
/// Each container is applied with the same extension methods code uses (<c>UseBlobManagementContainer</c>, the presets), so
/// the two cannot drift apart. The storage provider beneath is not declared here: a container takes the default
/// container's provider and its settings, which the host configures in code (<c>ConfigureDefault(c =&gt;
/// c.UseFileSystem(…))</c>).
/// <para>
/// Everything is checked while reading, so a mistake stops the host at startup: an unknown key, an unknown
/// <c>Kind</c> or preset, a value that is not a number or a boolean, a misspelt preset parameter.
/// </para>
/// </summary>
public class BlobManagementContainerSettings
{
    public const string SectionName = "BlobManagement";

    public const string ContainersSectionName = "Containers";

    private static readonly string[] KnownKeys = { "Kind", "PublicRead", "AllowedClientIds", "Preset", "PresetOptions" };

    protected BlobManagementContainerSettings(string containerName, Action<BlobContainerConfiguration> apply)
    {
        ContainerName = containerName;
        Apply = apply;
    }

    public string ContainerName { get; }

    /// <summary>Declares the container on its <see cref="BlobContainerConfiguration"/>.</summary>
    public Action<BlobContainerConfiguration> Apply { get; }

    /// <summary>
    /// The containers of <c>BlobManagement:Containers</c> in <paramref name="configuration"/>, checked; none when the section is
    /// missing. Throws <see cref="AbpException"/> on the first mistake.
    /// </summary>
    public static List<BlobManagementContainerSettings> Read(IConfiguration configuration, BlobManagementPresetOptions presets)
    {
        Check.NotNull(configuration, nameof(configuration));
        Check.NotNull(presets, nameof(presets));

        return configuration
            .GetSection(SectionName)
            .GetSection(ContainersSectionName)
            .GetChildren()
            .Select(section => ReadContainer(section, presets))
            .ToList();
    }

    protected static BlobManagementContainerSettings ReadContainer(IConfigurationSection section, BlobManagementPresetOptions presets)
    {
        var containerName = section.Key;
        var where = $"BlobManagement container '{containerName}' in configuration ({section.Path})";

        var unknownKey = section.GetChildren().Select(child => child.Key)
            .FirstOrDefault(key => !KnownKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (unknownKey != null)
        {
            throw new AbpException($"{where}: unknown key '{unknownKey}'. Known keys: {string.Join(", ", KnownKeys)}.");
        }

        var kind = BlobManagementContainerKind.System;
        var kindValue = section["Kind"];
        if (!kindValue.IsNullOrWhiteSpace() &&
            (!Enum.TryParse(kindValue, ignoreCase: true, out kind) || !Enum.IsDefined(typeof(BlobManagementContainerKind), kind)))
        {
            throw new AbpException($"{where}: '{kindValue}' is not a Kind. Use {string.Join(" or ", Enum.GetNames(typeof(BlobManagementContainerKind)))}.");
        }

        var publicRead = false;
        var publicReadValue = section["PublicRead"];
        if (!publicReadValue.IsNullOrWhiteSpace() && !bool.TryParse(publicReadValue, out publicRead))
        {
            throw new AbpException($"{where}: PublicRead must be true or false, not '{publicReadValue}'.");
        }

        var allowedClientIds = section.GetSection("AllowedClientIds").GetChildren()
            .Select(child => child.Value ?? string.Empty)
            .ToArray();

        Action<BlobContainerConfiguration>? applyPreset = null;
        var presetName = section["Preset"];
        if (!presetName.IsNullOrWhiteSpace())
        {
            if (!presets.Presets.TryGetValue(presetName!, out var preset))
            {
                throw new AbpException(
                    $"{where}: unknown preset '{presetName}'. Known presets: {string.Join(", ", presets.Presets.Keys)}.");
            }

            var parameters = section.GetSection("PresetOptions");
            applyPreset = container => preset(container, parameters);
        }
        else if (section.GetSection("PresetOptions").Exists())
        {
            throw new AbpException($"{where}: PresetOptions is given without a Preset.");
        }

        Action<BlobContainerConfiguration> apply = container =>
        {
            container.UseBlobManagementContainer(drive =>
            {
                drive.Kind = kind;
                drive.PublicRead = publicRead;
                drive.AllowedClientIds = allowedClientIds;
            });
            applyPreset?.Invoke(container);
        };

        // A dry run on a throwaway configuration, so that what only the extension methods check - a client id list on
        // a user drive, a preset parameter that is misspelt or out of range - stops the host now, not on the first
        // upload.
        try
        {
            apply(new BlobContainerConfiguration());
        }
        catch (Exception exception) when (exception is AbpException or InvalidOperationException)
        {
            throw new AbpException($"{where}: {exception.Message}", exception);
        }

        return new BlobManagementContainerSettings(containerName, apply);
    }
}
