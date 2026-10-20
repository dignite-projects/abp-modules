```json
//[doc-seo]
{
    "Description": "Write your own ABP BLOB pipeline contributor with the capped buffering, MIME type detection and per-container configuration this module uses."
}
```

# Writing Your Own Contributors

A contributor of your own is an ordinary ABP [pipeline contributor](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline#creating-a-pipeline-contributor): a class that implements `IBlobPipelineContributor`. This module does not change how you write one, but its public helpers save you the parts that are easy to get wrong: buffering the content without a limit, and detecting its type.

> Read [The Stream Ownership Contract](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline#the-stream-ownership-contract) of ABP first. A contributor that reads the content must leave a readable stream for the contributors after it and for the storage provider.

## Example: Rejecting SVG Images with Script

The following contributor rejects SVG images that contain a `<script>` element. It needs to read the content and then hand the same content on, so it uses `BlobStreamBuffering.EnsureSeekableAsync`:

````csharp
public class SvgScriptCheckContributor : IBlobPipelineContributor, ITransientDependency
{
    private readonly IMimeTypeDetector _mimeTypeDetector;

    public SvgScriptCheckContributor(IMimeTypeDetector mimeTypeDetector)
    {
        _mimeTypeDetector = mimeTypeDetector;
    }

    public async Task OnSavingAsync(BlobPipelineContext context)
    {
        // A seekable stream, capped at the container's limit
        var stream = await BlobStreamBuffering.EnsureSeekableAsync(
            context,
            BlobStreamBuffering.GetMaxBufferedBytes(context.Configuration));

        var mimeType = await _mimeTypeDetector.DetectAsync(stream, context.BlobName, context.CancellationToken);
        if (mimeType != "image/svg+xml")
        {
            return;
        }

        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            var markup = await reader.ReadToEndAsync(context.CancellationToken);
            if (markup.Contains("<script", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("MyProject:SvgWithScript");
            }
        }

        // Hand the content on from its start
        stream.Position = 0;
    }

    public Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }
}
````

Then add it to a container like any other contributor, after the size limit:

````csharp
options.Containers.Configure<PictureContainer>(container =>
{
    container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 12 * 1024 * 1024);
    container.PipelineContributors.Add<SvgScriptCheckContributor>();
});
````

The contributor validates without transforming, so it does not change the stored format, and it can be added to a container that already has BLOBs.

## BlobStreamBuffering

`BlobStreamBuffering` is a static class of the `Dignite.Abp.BlobStoring.Pipeline` package. Every method is bounded, so content is never buffered without a limit:

* `GetMaxBufferedBytes(configuration)`: The cap for a container: its `MaxSizeInBytes` when a [size limit](max-size.md) is configured, otherwise `BlobStoringPipelineConsts.DefaultMaxBufferedBytes` (100 MB).
* `EnsureSeekableAsync(context, maxBytes)`: Returns `context.BlobStream` when it is already seekable, at position `0` and not longer than `maxBytes` (a longer one fails with `ContentTooLarge` before it is read). Otherwise it copies the stream into memory, failing beyond `maxBytes`, and assigns the copy to `context.BlobStream`, so the pipeline owns and disposes it.
* `CopyToBufferAsync(source, maxBytes, cancellationToken)`: Copies a stream into a new `MemoryStream`, failing with `ContentTooLarge` as soon as more than `maxBytes` have been read, and before reading anything when a seekable source already reports a larger length. It leaves `source` open, disposes its own buffer on failure and returns the buffer at position `0`. A buffer you assign to `context.BlobStream` is disposed by the pipeline; one you do not assign is yours to dispose.
* `CreateContentTooLargeException(maxBytes)`: The `ContentTooLarge` business exception, with the limit as `MaxSizeInBytes` data, for a contributor that enforces a limit of its own.

## Per-Container Configuration

The contributors of this module keep their settings in the container configuration, so one contributor class serves every container with different settings. Do the same with the `SetConfiguration` and `GetConfigurationOrDefault` methods of `BlobContainerConfiguration`, behind an extension method of your own:

````csharp
public static class SvgScriptCheckContainerConfigurationExtensions
{
    public const string AllowScriptName = "MyProject.SvgScriptCheck.AllowScript";

    public static BlobContainerConfiguration AddSvgScriptCheckContributor(
        this BlobContainerConfiguration containerConfiguration,
        bool allowScript = false)
    {
        containerConfiguration.SetConfiguration(AllowScriptName, allowScript);
        containerConfiguration.PipelineContributors.TryAdd<SvgScriptCheckContributor>();
        return containerConfiguration;
    }
}
````

The contributor then reads the value with `context.Configuration.GetConfigurationOrDefault(AllowScriptName, false)`. `TryAdd` keeps the contributor from being added twice when the extension method is called again.

## See Also

* [BLOB Content Pipeline](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline)
* [MIME Type Detection](mime-type-detection.md)
