using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Public;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="BlobManagementPublicAppService"/> that runs <see cref="AfterLookup"/> once right after the real lookup
/// found the file - where an overwrite by another request would land between a read's lookup and its bytes. The lookup
/// itself is the service's own. Exposed as itself only, so it does not replace the real
/// <see cref="IBlobManagementPublicAppService"/>.
/// </summary>
[ExposeServices(typeof(RacingBlobManagementPublicAppService))]
public class RacingBlobManagementPublicAppService : BlobManagementPublicAppService
{
    public RacingBlobManagementPublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer)
        : base(fileDescriptorManager, imageResizer)
    {
    }

    /// <summary>Runs once, after the next lookup, then is cleared.</summary>
    public Func<Task>? AfterLookup { get; set; }

    /// <summary>How many times the read looked the file up.</summary>
    public int Lookups { get; private set; }

    protected override async Task<(FileDescriptor File, string Name)> GetPublicFileAsync(string containerName, string blobName)
    {
        Lookups++;
        var found = await base.GetPublicFileAsync(containerName, blobName);

        var afterLookup = AfterLookup;
        AfterLookup = null;
        if (afterLookup != null)
        {
            await afterLookup();
        }

        return found;
    }
}
