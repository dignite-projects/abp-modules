using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// The default <see cref="IFileStorer"/>. Transient (file-storing invariant §7): it captures no
/// per-request state, and the handlers it runs are resolved in a scope of their own per call.
/// </summary>
public class FileStorer : IFileStorer, ITransientDependency
{
    /// <summary>
    /// Compensation runs on its own bounded token: when the request itself was cancelled, reusing the
    /// request token would cancel the cleanup immediately and leave the partial blob behind.
    /// </summary>
    protected static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    protected const int CopyBufferSize = 81920;

    protected IServiceProvider ServiceProvider { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    protected ContainerNameValidator ContainerNameValidator { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    public FileStorer(
        IServiceProvider serviceProvider,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider configurationProvider,
        ContainerNameValidator containerNameValidator,
        IMimeTypeDetector mimeTypeDetector)
    {
        ServiceProvider = serviceProvider;
        BlobContainerFactory = blobContainerFactory;
        ConfigurationProvider = configurationProvider;
        ContainerNameValidator = containerNameValidator;
        MimeTypeDetector = mimeTypeDetector;
    }

    public virtual async Task<StoredFileInfo> StoreAsync(
        string containerName,
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(stream, nameof(stream));
        Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
        ContainerNameValidator.Validate(containerName);
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = ConfigurationProvider.Get(containerName);
        var maxFileSizeInBytes = GetMaxFileSizeInBytes(configuration);

        // Every stream created while storing (the capped buffer and whatever the handlers replace it
        // with) is disposed at the end. The caller's stream is never disposed.
        var ownedStreams = new List<Stream>();
        try
        {
            var buffer = await CopyToBufferAsync(stream, maxFileSizeInBytes, cancellationToken);
            ownedStreams.Add(buffer);

            var mimeType = await MimeTypeDetector.DetectAsync(buffer, fileName, cancellationToken);

            var content = await RunHandlersAsync(
                configuration, fileName, mimeType, buffer, ownedStreams, cancellationToken);

            if (!ReferenceEquals(content, buffer))
            {
                if (!content.CanSeek)
                {
                    content = await CopyToBufferAsync(content, maxFileSizeInBytes, cancellationToken);
                    ownedStreams.Add(content);
                }

                // A transform may have changed the format (a resize keeps it, a converter would not).
                mimeType = await MimeTypeDetector.DetectAsync(content, fileName, cancellationToken);
            }

            content.Position = 0;
            var size = content.Length;
            var hash = await ComputeHashAsync(content, cancellationToken);
            var blobName = await CreateBlobNameAsync(configuration);

            await SaveBlobAsync(containerName, blobName, content, cancellationToken);

            return new StoredFileInfo(blobName, size, mimeType, hash);
        }
        finally
        {
            foreach (var ownedStream in ownedStreams)
            {
                await ownedStream.DisposeAsync();
            }
        }
    }

    public virtual async Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ContainerNameValidator.Validate(containerName);
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName), FileConsts.MaxBlobNameLength);

        return await BlobContainerFactory.Create(containerName).DeleteAsync(blobName, cancellationToken);
    }

    /// <summary>
    /// The container's <c>AddFileSizeLimitHandler</c> limit, or <see cref="FileConsts.DefaultMaxFileSizeInBytes"/>
    /// for a container without one, so no upload is ever copied without a cap.
    /// </summary>
    protected virtual long GetMaxFileSizeInBytes(BlobContainerConfiguration configuration)
    {
        var configured = configuration.GetFileSizeLimitConfiguration().MaxFileSizeInBytes;
        return configured > 0 ? configured : FileConsts.DefaultMaxFileSizeInBytes;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into memory, failing as soon as more than
    /// <paramref name="maxFileSizeInBytes"/> bytes have been read (and before reading anything when a
    /// seekable source already reports a larger length). The handlers and the signature probe need a
    /// re-readable stream, so the content is buffered — but never beyond the container's limit.
    /// </summary>
    protected virtual async Task<MemoryStream> CopyToBufferAsync(
        Stream source,
        long maxFileSizeInBytes,
        CancellationToken cancellationToken)
    {
        var knownLength = source.CanSeek ? Math.Max(0, source.Length - source.Position) : 0;
        if (knownLength > maxFileSizeInBytes)
        {
            throw CreateFileTooLargeException(maxFileSizeInBytes);
        }

        var buffer = new MemoryStream((int)Math.Min(knownLength, Array.MaxLength));
        var chunk = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(chunk.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxFileSizeInBytes)
                {
                    throw CreateFileTooLargeException(maxFileSizeInBytes);
                }

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Runs the container's <see cref="IFileHandler"/>s in their configured order over
    /// <paramref name="content"/> and returns the resulting stream.
    /// </summary>
    protected virtual async Task<Stream> RunHandlersAsync(
        BlobContainerConfiguration configuration,
        string fileName,
        string mimeType,
        Stream content,
        ICollection<Stream> ownedStreams,
        CancellationToken cancellationToken)
    {
        var handlerTypes = configuration.GetConfigurationOrDefault<ITypeList<IFileHandler>>(
            BlobContainerConfigurationNames.FileHandlers);

        if (handlerTypes == null || handlerTypes.Count == 0)
        {
            return content;
        }

        var context = new FileHandlerContext(fileName, mimeType, content, configuration, cancellationToken);

        using var scope = ServiceProvider.CreateScope();
        foreach (var handlerType in handlerTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.BlobStream.CanSeek)
            {
                context.BlobStream.Position = 0;
            }

            var handler = (IFileHandler)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.ExecuteAsync(context);

            if (!ownedStreams.Any(owned => ReferenceEquals(owned, context.BlobStream)))
            {
                ownedStreams.Add(context.BlobStream);
            }
        }

        return context.BlobStream;
    }

    /// <summary>
    /// SHA-256 of the content as upper-case hex (the same format as <c>StreamExtensions.Sha256</c>);
    /// the stream is rewound afterwards.
    /// </summary>
    protected virtual async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Resolves the container's <see cref="IBlobNameGenerator"/> (set with
    /// <c>SetBlobNameGenerator&lt;T&gt;()</c>; <see cref="RandomBlobNameGenerator"/> otherwise).
    /// </summary>
    protected virtual async Task<string> CreateBlobNameAsync(BlobContainerConfiguration configuration)
    {
        var generatorType = configuration.GetBlobNameGeneratorType();
        var generator = (IBlobNameGenerator)ServiceProvider.GetRequiredService(generatorType);
        var blobName = await generator.Create();

        return Check.NotNullOrWhiteSpace(blobName, nameof(blobName), FileConsts.MaxBlobNameLength);
    }

    /// <summary>
    /// Saves the content without overwriting. When the save fails after it may have written bytes,
    /// the blob is deleted again (file-storing invariant §4) — except when the failure is that the
    /// name already exists: that blob belongs to someone else and must not be touched.
    /// </summary>
    protected virtual async Task SaveBlobAsync(
        string containerName,
        string blobName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var blobContainer = BlobContainerFactory.Create(containerName);
        try
        {
            await blobContainer.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);
        }
        catch (BlobAlreadyExistsException)
        {
            throw;
        }
        catch
        {
            await TryDeleteBlobAsync(blobContainer, blobName);
            throw;
        }
    }

    protected virtual async Task TryDeleteBlobAsync(IBlobContainer blobContainer, string blobName)
    {
        using var compensationCts = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await blobContainer.DeleteAsync(blobName, compensationCts.Token);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    protected virtual BusinessException CreateFileTooLargeException(long maxFileSizeInBytes)
    {
        var maxFileSizeInMegabytes = maxFileSizeInBytes / (1024 * 1024);
        return new BusinessException(
            code: FileErrorCodes.Files.FileTooLarge,
            message: "File object is too large",
            details: $"The file object size cannot exceed {maxFileSizeInMegabytes} MB!");
    }
}
