using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The sortings a file listing accepts. The value reaches a dynamic <c>OrderBy</c>, so anything but a known property
/// and direction is refused (<see cref="BlobManagementErrorCodes.SortingNotSupported"/>) rather than passed through.
/// </summary>
public static class FileDescriptorSorting
{
    public const string Default = nameof(FileDescriptor.Name) + " asc, " + nameof(FileDescriptor.Id) + " asc";

    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(FileDescriptor.Name),
        nameof(FileDescriptor.Size),
        nameof(FileDescriptor.MimeType),
        nameof(FileDescriptor.CreationTime),
        nameof(FileDescriptor.LastModificationTime)
    };

    public static string Normalize(string? sorting)
    {
        if (sorting.IsNullOrWhiteSpace())
        {
            return Default;
        }

        var parts = sorting!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 2 || !AllowedFields.TryGetValue(parts[0], out var field) ||
            (parts.Length == 2 &&
             !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) &&
             !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(BlobManagementErrorCodes.SortingNotSupported).WithData("Sorting", sorting);
        }

        var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
        // The property's own name: a client sends "name" as readily as "Name". The id ends the order: names, sizes and
        // types repeat, and without a last word two pages of one listing could share a file or miss one.
        return $"{field} {direction}, {nameof(FileDescriptor.Id)} asc";
    }
}
