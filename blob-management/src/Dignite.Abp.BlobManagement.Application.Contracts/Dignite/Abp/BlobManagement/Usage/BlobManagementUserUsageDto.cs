using System;

namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementUserUsageDto
{
    /// <summary>The user the files are counted for; <c>null</c> gathers the files recorded for no user.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The user's name, where the host has a user lookup and still knows the user; otherwise <c>null</c>.</summary>
    public string? UserName { get; set; }

    /// <summary>The sizes of the user's files, whether or not they share their bytes with another file.</summary>
    public long UploadedBytes { get; set; }

    public long FileCount { get; set; }
}
