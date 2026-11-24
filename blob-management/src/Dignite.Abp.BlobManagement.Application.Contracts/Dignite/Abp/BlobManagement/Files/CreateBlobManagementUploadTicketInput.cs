using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// What an upload ticket lets a browser store: one file, in one place of a container, for one user. Everything here is
/// written into the ticket; the browser that holds it can change none of it.
/// </summary>
public class CreateBlobManagementUploadTicketInput : IValidatableObject
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory to file it in; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>
    /// The user the upload is for: the file's <c>CreatorId</c>, which the business system checks against its current
    /// user before it confirms the file. <c>null</c>: the application's current user - in process as
    /// <c>ICurrentUser</c> has it when the ticket is issued, over HTTP as <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>
    /// sends it, since the client's token carries no user.
    /// </summary>
    public Guid? UploaderId { get; set; }

    /// <summary>
    /// The largest file the ticket takes, in bytes, and the limit of the upload's request body; <c>null</c>, or more
    /// than the container takes, is the container's own limit.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? MaxSize { get; set; }

    /// <summary>
    /// The content types the ticket takes, detected from the bytes (<c>image/png</c>, or <c>image/*</c> for a kind);
    /// empty takes whatever the container takes. The container's own rules apply as well: the ticket takes what both
    /// allow, and one that would take nothing is refused.
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    /// <summary>A <c>type/subtype</c> or <c>type/*</c>, in RFC 9110's token characters - what a container's list takes.</summary>
    private static readonly Regex ContentTypePattern =
        new(@"^[!#$%&'+.^_`|~0-9A-Za-z-]+/(\*|[!#$%&'+.^_`|~0-9A-Za-z-]+)$", RegexOptions.CultureInvariant);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var type in AllowedContentTypes ?? new List<string>())
        {
            if (!type.IsNullOrWhiteSpace() && !ContentTypePattern.IsMatch(type.Trim()))
            {
                yield return new ValidationResult(
                    $"'{type}' is not a content type: type/subtype or type/*.",
                    new[] { nameof(AllowedContentTypes) });
            }
        }
    }

    /// <summary>
    /// The largest width a picture uploaded with the ticket is stored at, in pixels; <c>null</c> leaves it to the
    /// container. A wider picture is scaled down, keeping its aspect ratio - never cropped, never enlarged - and the
    /// container's own box still applies: each side is the smaller of the two.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? ImageMaxWidth { get; set; }

    /// <summary>The largest height a picture uploaded with the ticket is stored at, in pixels; <c>null</c> leaves it to the container.</summary>
    [Range(1, int.MaxValue)]
    public int? ImageMaxHeight { get; set; }

    /// <summary>
    /// How long the ticket is valid, in seconds; <c>null</c> is <c>BlobManagementUploadOptions.TicketLifetime</c>, and
    /// a lifetime past <c>BlobManagementUploadOptions.MaxTicketLifetime</c> is cut to it.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? LifetimeInSeconds { get; set; }
}
