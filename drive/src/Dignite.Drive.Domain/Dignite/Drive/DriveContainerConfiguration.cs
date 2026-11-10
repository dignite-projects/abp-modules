using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive;

/// <summary>
/// What makes a BLOB container one of the Drive's, kept in its <see cref="BlobContainerConfiguration"/> on the Drive
/// host. Set it with <see cref="DriveContainerConfigurationExtensions.UseDriveContainer"/>; the storage provider
/// beneath (<c>UseFileSystem</c>, <c>UseAzure</c>, …) and the pipeline contributors are configured on the same
/// container as usual.
/// </summary>
public class DriveContainerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public DriveContainerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>Whether the container was declared with <c>UseDriveContainer</c>.</summary>
    public bool IsDriveContainer => _containerConfiguration.GetConfigurationOrNull(DriveContainerConfigurationNames.Kind) != null;

    /// <summary>Which entry point the container serves. Default: <see cref="DriveContainerKind.System"/>.</summary>
    public DriveContainerKind Kind
    {
        get => _containerConfiguration.GetConfigurationOrDefault(DriveContainerConfigurationNames.Kind, DriveContainerKind.System);
        set => _containerConfiguration.SetConfiguration(DriveContainerConfigurationNames.Kind, value);
    }

    /// <summary>Whether anyone may read the container's files without signing in. Default: <c>false</c>.</summary>
    public bool PublicRead
    {
        get => _containerConfiguration.GetConfigurationOrDefault(DriveContainerConfigurationNames.PublicRead, false);
        set => _containerConfiguration.SetConfiguration(DriveContainerConfigurationNames.PublicRead, value);
    }

    /// <summary>
    /// The OAuth clients that may read, write and delete in a system container through the BLOB API. Empty: no
    /// remote caller may (in-process callers are trusted).
    /// </summary>
    public string[] AllowedClientIds
    {
        get => _containerConfiguration.GetConfigurationOrDefault<string[]>(DriveContainerConfigurationNames.AllowedClientIds) ?? Array.Empty<string>();
        set => _containerConfiguration.SetConfiguration(DriveContainerConfigurationNames.AllowedClientIds, value ?? Array.Empty<string>());
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (!Enum.IsDefined(typeof(DriveContainerKind), Kind))
        {
            throw new AbpException($"'{Kind}' is not a {nameof(DriveContainerKind)}.");
        }

        if (Kind != DriveContainerKind.System && AllowedClientIds.Length > 0)
        {
            throw new AbpException(
                $"{nameof(AllowedClientIds)} apply to system containers only: users reach a user drive through the file API, not the BLOB API.");
        }

        if (AllowedClientIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new AbpException($"{nameof(AllowedClientIds)} cannot contain an empty client id.");
        }
    }
}
