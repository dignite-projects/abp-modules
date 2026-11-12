using System;

namespace Dignite.Abp.BlobManagement.Files;

public class MoveBlobManagementFileInput
{
    /// <summary>The directory to file it in, of the same container; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }
}
