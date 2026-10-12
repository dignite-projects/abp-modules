using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Testing;

namespace Dignite.Abp.BlobStoring.Pipeline;

public abstract class BlobStoringPipelineTestBase : AbpIntegratedTest<BlobStoringPipelineTestModule>
{
    protected IBlobContainerFactory BlobContainerFactory => GetRequiredService<IBlobContainerFactory>();

    protected IBlobContainer Container(string name) => BlobContainerFactory.Create(name);

    /// <summary>
    /// The bytes the provider stored, bypassing the container's pipeline; <c>null</c> when nothing was stored.
    /// </summary>
    protected async Task<byte[]?> GetStoredBytesAsync(string containerName, string blobName)
    {
        var configuration = GetRequiredService<IBlobContainerConfigurationProvider>().Get(containerName);
        await using var stream = await GetRequiredService<MemoryBlobProvider>()
            .GetOrNullAsync(new BlobProviderGetArgs(containerName, configuration, blobName));

        if (stream == null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    protected static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }
}
