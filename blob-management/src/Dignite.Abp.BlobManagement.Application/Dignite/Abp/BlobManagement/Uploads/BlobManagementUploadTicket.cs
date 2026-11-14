using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// What an upload ticket carries, signed by <see cref="Tickets.BlobManagementTicketProtector"/>: everything the anonymous
/// upload is allowed, so the request that brings the file is trusted for nothing but the bytes. Its expiration is the
/// protector's.
/// </summary>
public class BlobManagementUploadTicket
{
    /// <summary>The Data Protection purpose of upload tickets.</summary>
    public const string Purpose = "Dignite.Abp.BlobManagement.UploadTicket";

    /// <summary>The tenant the file is stored for - the ticket's, not the request's.</summary>
    public Guid? TenantId { get; set; }

    public string ContainerName { get; set; } = default!;

    public Guid? DirectoryId { get; set; }

    /// <summary>The id the file takes, given when the ticket is issued: a second upload with the ticket collides on it.</summary>
    public Guid FileId { get; set; }

    /// <summary>The user the upload is for, recorded as the file's <c>CreatorId</c>.</summary>
    public Guid? UploaderId { get; set; }

    /// <summary>The OAuth client that asked for the ticket, recorded as the file's writer; <c>null</c> in process.</summary>
    public string? ClientId { get; set; }

    /// <summary>The largest file the ticket takes, in bytes.</summary>
    public long MaxSize { get; set; }

    /// <summary>The content types it takes (<c>image/png</c>, <c>image/*</c>); empty takes any.</summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    /// <summary>Whether <paramref name="contentType"/>, detected from the bytes, is one the ticket takes.</summary>
    public virtual bool Allows(string contentType)
    {
        if (AllowedContentTypes.Count == 0)
        {
            return true;
        }

        return AllowedContentTypes.Any(allowed =>
            allowed.EndsWith("/*", StringComparison.Ordinal)
                ? contentType.StartsWith(allowed[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase));
    }
}
