using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring.Imaging;

public class InMemoryBlobProvider : BlobProviderBase, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public byte[]? Get(string containerName, string blobName) =>
        _blobs.TryGetValue(containerName + "/" + blobName, out var bytes) ? bytes : null;

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        using var copy = new MemoryStream();
        await args.BlobStream.CopyToAsync(copy, args.CancellationToken);
        _blobs[args.ContainerName + "/" + args.BlobName] = copy.ToArray();
    }

    public override Task<bool> DeleteAsync(BlobProviderDeleteArgs args) =>
        Task.FromResult(_blobs.TryRemove(args.ContainerName + "/" + args.BlobName, out _));

    public override Task<bool> ExistsAsync(BlobProviderExistsArgs args) =>
        Task.FromResult(_blobs.ContainsKey(args.ContainerName + "/" + args.BlobName));

    public override Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args) =>
        Task.FromResult<Stream?>(Get(args.ContainerName, args.BlobName) is { } bytes ? new MemoryStream(bytes) : null);
}
