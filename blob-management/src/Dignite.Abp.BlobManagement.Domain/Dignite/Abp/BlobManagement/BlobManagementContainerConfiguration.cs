using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What makes a BLOB container one of the BlobManagement's, kept in its <see cref="BlobContainerConfiguration"/> on the Blob Management
/// host. Set it with <see cref="BlobManagementContainerConfigurationExtensions.UseBlobManagementContainer"/>; the storage provider
/// beneath (<c>UseFileSystem</c>, <c>UseAzure</c>, …) and the pipeline contributors are configured on the same
/// container as usual.
/// </summary>
public class BlobManagementContainerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public BlobManagementContainerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>Whether the container was declared with <c>UseBlobManagementContainer</c>.</summary>
    public bool IsBlobManagementContainer => _containerConfiguration.GetConfigurationOrNull(BlobManagementContainerConfigurationNames.Kind) != null;

    /// <summary>Which entry point the container serves. Default: <see cref="BlobManagementContainerKind.System"/>.</summary>
    public BlobManagementContainerKind Kind
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementContainerConfigurationNames.Kind, BlobManagementContainerKind.System);
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.Kind, value);
    }

    /// <summary>Whether anyone may read the container's files without signing in. Default: <c>false</c>.</summary>
    public bool PublicRead
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementContainerConfigurationNames.PublicRead, false);
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.PublicRead, value);
    }

    /// <summary>
    /// The OAuth clients that may read, write and delete in a system container through the BLOB API. Empty: no
    /// remote caller may (in-process callers are trusted).
    /// </summary>
    public string[] AllowedClientIds
    {
        get => _containerConfiguration.GetConfigurationOrDefault<string[]>(BlobManagementContainerConfigurationNames.AllowedClientIds) ?? Array.Empty<string>();
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.AllowedClientIds, value ?? Array.Empty<string>());
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (!Enum.IsDefined(typeof(BlobManagementContainerKind), Kind))
        {
            throw new AbpException($"'{Kind}' is not a {nameof(BlobManagementContainerKind)}.");
        }

        if (Kind != BlobManagementContainerKind.System && AllowedClientIds.Length > 0)
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
