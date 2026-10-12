using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The per-container configuration of <see cref="AllowedContentTypesContributor"/>.
/// </summary>
public class AllowedContentTypesContributorConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public AllowedContentTypesContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The MIME types the container accepts, compared case-insensitively with the type
    /// <see cref="IMimeTypeDetector"/> detects from the content: exact types (<c>image/png</c>) or a wildcard
    /// over a top-level type (<c>image/*</c>). Must not be empty.
    /// </summary>
    public string[] AllowedContentTypes
    {
        get => _containerConfiguration.GetConfigurationOrDefault<string[]>(BlobStoringPipelineConfigurationNames.AllowedContentTypes)
               ?? Array.Empty<string>();
        set => _containerConfiguration.SetConfiguration(
            BlobStoringPipelineConfigurationNames.AllowedContentTypes,
            Check.NotNull(value, nameof(value)).ToArray());
    }

    /// <summary>
    /// Whether content <see cref="IMimeTypeDetector"/> cannot identify
    /// (<see cref="MimeTypeDetector.DefaultMimeType"/>) is accepted. Default: <c>false</c>. Listing
    /// <c>application/octet-stream</c> itself in <see cref="AllowedContentTypes"/> has the same effect;
    /// <c>application/*</c> does not.
    /// </summary>
    public bool AllowUnidentified
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringPipelineConfigurationNames.AllowUnidentified, false);
        set => _containerConfiguration.SetConfiguration(BlobStoringPipelineConfigurationNames.AllowUnidentified, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        var contentTypes = AllowedContentTypes;
        if (contentTypes.Length == 0)
        {
            throw new AbpException(
                $"{nameof(AllowedContentTypesContributor)} requires at least one allowed content type; configure it with AddAllowedContentTypesContributor.");
        }

        var invalid = contentTypes.Where(contentType => !IsValidContentType(contentType)).ToList();
        if (invalid.Count > 0)
        {
            throw new AbpException(
                $"{nameof(AllowedContentTypesContributor)}: '{string.Join("', '", invalid)}' is not a 'type/subtype' or 'type/*' content type.");
        }
    }

    private static bool IsValidContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var parts = contentType.Split('/');
        return parts.Length == 2 &&
               IsToken(parts[0]) &&
               (parts[1] == "*" || IsToken(parts[1]));
    }

    private static bool IsToken(string value)
    {
        // RFC 9110 token characters: visible ASCII except delimiters (and '*', which is only valid as the
        // whole subtype).
        return value.Length > 0 && value.All(c => c is > ' ' and < (char)0x7F && "()<>@,;:\\\"/[]?={}*".IndexOf(c) < 0);
    }
}
