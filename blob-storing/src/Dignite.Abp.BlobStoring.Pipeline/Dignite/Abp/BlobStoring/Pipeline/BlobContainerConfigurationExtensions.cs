using System;
using System.Collections.Generic;
using System.IO;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Adds this package's contributors to a container. Each <c>Add…Contributor</c> applies the configuration,
/// validates it (throwing <see cref="AbpException"/> on an invalid setup) and appends the contributor to
/// <see cref="BlobContainerConfiguration.PipelineContributors"/> once; calling it again only updates the
/// configuration. Contributors run in the order they were added while saving (in reverse while reading).
/// </summary>
public static class BlobContainerConfigurationExtensions
{
    public static MaxSizeContributorConfiguration GetMaxSizeContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new MaxSizeContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="MaxSizeContributor"/>; set <see cref="MaxSizeContributorConfiguration.MaxSizeInBytes"/>.
    /// Add it first, so later contributors work on its bounded, seekable copy.
    /// </summary>
    public static BlobContainerConfiguration AddMaxSizeContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<MaxSizeContributorConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new MaxSizeContributorConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<MaxSizeContributor>();
        return containerConfiguration;
    }

    public static AllowedContentTypesContributorConfiguration GetAllowedContentTypesContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new AllowedContentTypesContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="AllowedContentTypesContributor"/>; set
    /// <see cref="AllowedContentTypesContributorConfiguration.AllowedContentTypes"/>. Add it before any
    /// transforming contributor.
    /// </summary>
    public static BlobContainerConfiguration AddAllowedContentTypesContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<AllowedContentTypesContributorConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new AllowedContentTypesContributorConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<AllowedContentTypesContributor>();
        return containerConfiguration;
    }

    public static GZipContributorConfiguration GetGZipContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new GZipContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="GZipContributor"/>, optionally setting
    /// <see cref="GZipContributorConfiguration.CompressionLevel"/>. It changes the stored format: do not add it
    /// to (or remove it from) a container that already has BLOBs.
    /// </summary>
    public static BlobContainerConfiguration AddGZipContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<GZipContributorConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new GZipContributorConfiguration(containerConfiguration);
        configureAction?.Invoke(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<GZipContributor>();
        return containerConfiguration;
    }

    /// <summary>
    /// The MIME type that <paramref name="content"/>, detected as <paramref name="contentType"/>, is stored as in this
    /// container - what a read of the blob through the container returns: <paramref name="contentType"/> itself, unless a
    /// contributor that converts it declared otherwise with <c>SetStoredContentType</c> (for example <c>image/jpeg</c>
    /// stored as <c>image/webp</c>) and, for a declaration with a condition, the condition holds for this content.
    /// Compared case-insensitively.
    /// <para>
    /// A declaration read from the configuration before anything is saved, for a caller that names its blobs or records
    /// their type before the save: it is not a result of the save. <paramref name="content"/> is what the caller is about
    /// to save; it is read only when the declaration has a condition, and must then be seekable - the condition reads it
    /// from position 0, and its position is restored. A caller that has to know what was actually stored detects it from
    /// the stored bytes after the save.
    /// </para>
    /// </summary>
    public static string GetStoredContentType(
        this BlobContainerConfiguration containerConfiguration,
        string contentType,
        Stream content)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNullOrWhiteSpace(contentType, nameof(contentType));
        Check.NotNull(content, nameof(content));

        var declarations = containerConfiguration
            .GetConfigurationOrDefault<IReadOnlyDictionary<string, StoredContentTypeDeclaration>?>(BlobStoringPipelineConfigurationNames.StoredContentTypes);
        if (declarations == null || !declarations.TryGetValue(contentType, out var declaration))
        {
            return contentType;
        }

        if (declaration.Condition == null)
        {
            return declaration.StoredContentType;
        }

        if (!content.CanSeek)
        {
            throw new ArgumentException("The content must be seekable for a conditional declaration to be applied to it.", nameof(content));
        }

        var position = content.Position;
        try
        {
            content.Position = 0;
            return declaration.Condition(containerConfiguration, content) ? declaration.StoredContentType : contentType;
        }
        finally
        {
            content.Position = position;
        }
    }

    /// <summary>
    /// Declares that the container stores every content detected as <paramref name="contentType"/> as
    /// <paramref name="storedContentType"/>: called by whoever adds a contributor that converts every such content, so
    /// <c>GetStoredContentType</c> tells a caller before the save. A contributor that converts only some of it declares
    /// the rule with the overload that takes a condition. One step: a type that is itself converted is not followed.
    /// Declaring a type again replaces its earlier declaration; declarations on the default container apply to the others
    /// until a container declares its own.
    /// </summary>
    public static BlobContainerConfiguration SetStoredContentType(
        this BlobContainerConfiguration containerConfiguration,
        string contentType,
        string storedContentType)
    {
        return containerConfiguration.SetStoredContentType(contentType, storedContentType, condition: null);
    }

    /// <summary>
    /// Declares that the container stores content detected as <paramref name="contentType"/> as
    /// <paramref name="storedContentType"/> when <paramref name="condition"/> holds for it (<c>null</c>: always), so
    /// <c>GetStoredContentType</c> tells a caller before the save, from the content it is about to save.
    /// <para>
    /// The condition is the contributor's rule, and the contributor applies the same one, by reading this declaration: it
    /// is called with the container's configuration and the content, seekable and at position 0, and may move the
    /// position. A caller evaluates it on the content as it is about to save it, the contributor on the content as the
    /// contributors before it left it, so it may depend only on what those keep (a picture's header, not the length when
    /// a contributor before it strips metadata), and must not depend on anything computed while saving - whether the
    /// result came out smaller cannot be declared. It should read the header only: it runs before the save and again in
    /// the pipeline.
    /// </para>
    /// </summary>
    public static BlobContainerConfiguration SetStoredContentType(
        this BlobContainerConfiguration containerConfiguration,
        string contentType,
        string storedContentType,
        Func<BlobContainerConfiguration, Stream, bool>? condition)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNullOrWhiteSpace(contentType, nameof(contentType));
        Check.NotNullOrWhiteSpace(storedContentType, nameof(storedContentType));

        // A new dictionary each time: the one read may be the default container's, which other containers share.
        var declarations = new Dictionary<string, StoredContentTypeDeclaration>(StringComparer.OrdinalIgnoreCase);
        var current = containerConfiguration
            .GetConfigurationOrDefault<IReadOnlyDictionary<string, StoredContentTypeDeclaration>?>(BlobStoringPipelineConfigurationNames.StoredContentTypes);
        if (current != null)
        {
            foreach (var (from, declaration) in current)
            {
                declarations[from] = declaration;
            }
        }

        declarations[contentType] = new StoredContentTypeDeclaration(storedContentType, condition);
        containerConfiguration.SetConfiguration(
            BlobStoringPipelineConfigurationNames.StoredContentTypes,
            (IReadOnlyDictionary<string, StoredContentTypeDeclaration>)declarations);
        return containerConfiguration;
    }
}
