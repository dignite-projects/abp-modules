using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.FileExplorer.Files;

public static class FileDescriptorSorting
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreationTime",
        "Name",
        "Size",
        "BlobName"
    };

    public static string Normalize(string sorting, string defaultSorting)
    {
        if (sorting.IsNullOrWhiteSpace())
        {
            return defaultSorting;
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 2 || !AllowedFields.Contains(parts[0]) ||
            parts.Length == 2 &&
            !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                code: FileExplorerErrorCodes.Files.InvalidSorting,
                message: "The requested sorting is not supported.");
        }

        var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
        return $"{parts[0]} {direction}";
    }
}
