using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Public;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="BlobManagementPublicAppService"/> that reads any container as if it were read publicly, and runs <see cref="AfterLookup"/> once right after it found the file -
/// where an overwrite by another request would land between a read's lookup and its bytes. Exposed as itself only, so
/// it does not replace the real <see cref="IBlobManagementPublicAppService"/>.
/// </summary>
[ExposeServices(typeof(RacingBlobManagementPublicAppService))]
public class RacingBlobManagementPublicAppService : BlobManagementPublicAppService
{
    public RacingBlobManagementPublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer,
        IOptions<BlobManagementPublicReadOptions> options)
        : base(fileDescriptorManager, imageResizer, options)
    {
    }

    /// <summary>Runs once, after the next lookup, then is cleared.</summary>
    public Func<Task>? AfterLookup { get; set; }

    /// <summary>How many times the read looked the file up.</summary>
    public int Lookups { get; private set; }

    protected override async Task<(FileDescriptor File, string Name)> GetPublicFileAsync(string containerName, string blobName)
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

        return (file, file.Name);
    }
}
