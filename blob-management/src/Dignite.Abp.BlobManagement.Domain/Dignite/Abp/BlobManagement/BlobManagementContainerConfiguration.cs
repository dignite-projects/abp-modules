using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What makes a BLOB container a managed one, kept in its <see cref="BlobContainerConfiguration"/> on the Blob
/// Management host. Set it with <see cref="BlobManagementContainerConfigurationExtensions.UseBlobManagementContainer"/>;
/// the storage provider beneath (<c>UseFileSystem</c>, <c>UseAzure</c>, …) and the pipeline contributors are configured
/// on the same container as usual.
/// <para>
/// There is one kind of managed container. An application saves BLOBs in it under names of its own (the BLOB storing
/// provider, the BLOB API), people upload files to it (the file API), and the back office shows both.
/// </para>
/// </summary>
public class BlobManagementContainerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public BlobManagementContainerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>Whether the container was declared with <c>UseBlobManagementContainer</c>.</summary>
    public bool IsManaged
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementContainerConfigurationNames.Managed, false);
        internal set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.Managed, value);
    }

    /// <summary>Whether anyone may read the container's files without signing in. Default: <c>false</c>.</summary>
    public bool PublicRead
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementContainerConfigurationNames.PublicRead, false);
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.PublicRead, value);
    }

    /// <summary>
    /// The OAuth clients that may read, write and delete in the container through the BLOB API. Empty: no remote client
    /// may (in-process callers are trusted).
    /// </summary>
    public string[] AllowedClientIds
    {
        get => _containerConfiguration.GetConfigurationOrDefault<string[]>(BlobManagementContainerConfigurationNames.AllowedClientIds) ?? Array.Empty<string>();
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.AllowedClientIds, value ?? Array.Empty<string>());
    }

    /// <summary>
    /// Whether the container has a directory tree for the files people upload. Default: <c>false</c> - the files of the
    /// container are one flat list, and every directory operation is refused
    /// (<see cref="BlobManagementErrorCodes.DirectoriesNotAllowed"/>).
    /// </summary>
    public bool AllowDirectories
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementContainerConfigurationNames.AllowDirectories, false);
        set => _containerConfiguration.SetConfiguration(BlobManagementContainerConfigurationNames.AllowDirectories, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (AllowedClientIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new AbpException($"{nameof(AllowedClientIds)} cannot contain an empty client id.");
        }
    }
}
