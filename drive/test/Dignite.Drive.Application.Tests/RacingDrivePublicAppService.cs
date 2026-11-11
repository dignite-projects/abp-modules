using System;
using System.Threading.Tasks;
using Dignite.Drive.Public;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Imaging;

namespace Dignite.Drive;

/// <summary>
/// A <see cref="DrivePublicAppService"/> that reads any system container as if it were read publicly (the application
/// tests declare no public system container), and runs <see cref="AfterLookup"/> once right after it found the file -
/// where an overwrite by another request would land between a read's lookup and its bytes. Exposed as itself only, so
/// it does not replace the real <see cref="IDrivePublicAppService"/>.
/// </summary>
[ExposeServices(typeof(RacingDrivePublicAppService))]
public class RacingDrivePublicAppService : DrivePublicAppService
{
    public RacingDrivePublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer,
        IOptions<DrivePublicReadOptions> options)
        : base(fileDescriptorManager, imageResizer, options)
    {
    }

    /// <summary>Runs once, after the next lookup, then is cleared.</summary>
    public Func<Task>? AfterLookup { get; set; }

    /// <summary>How many times the read looked the file up.</summary>
    public int Lookups { get; private set; }

    protected override async Task<FileDescriptor> GetPublicFileAsync(string containerName, string blobName)
    {
        Lookups++;
        var file = await FileDescriptorManager.FindAsync(containerName, blobName)
                   ?? throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");

        var afterLookup = AfterLookup;
        AfterLookup = null;
        if (afterLookup != null)
        {
            await afterLookup();
        }

        return file;
    }
}
