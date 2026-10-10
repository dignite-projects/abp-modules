using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// In-memory blob provider that records deletes and can fail after it has written the bytes.
/// </summary>
public class FakeBlobProvider : BlobProviderBase, ISingletonDependency
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new();

    public ConcurrentQueue<string> DeletedBlobNames { get; } = new();

    /// <summary>
    /// Runs after the bytes were written, to simulate a provider failing (or being cancelled) mid-save.
    /// </summary>
    public Func<BlobProviderSaveArgs, Task>? AfterWrite { get; set; }

    public byte[]? Get(string containerName, string blobName)
    {
        return Blobs.TryGetValue(Key(containerName, blobName), out var bytes) ? bytes : null;
    }

    public void Put(string containerName, string blobName, byte[] bytes)
    {
        Blobs[Key(containerName, blobName)] = bytes;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var key = Key(args.ContainerName, args.BlobName);
        if (!args.OverrideExisting && Blobs.ContainsKey(key))
        {
            throw new BlobAlreadyExistsException($"'{args.BlobName}' already exists in '{args.ContainerName}'.");
        }

        using var copy = new MemoryStream();
        await args.BlobStream.CopyToAsync(copy, args.CancellationToken);
        Blobs[key] = copy.ToArray();

        if (AfterWrite != null)
        {
            await AfterWrite(args);
        }
    }

    public override Task<bool> DeleteAsync(BlobProviderDeleteArgs args)
    {
        DeletedBlobNames.Enqueue(args.BlobName);
        return Task.FromResult(Blobs.TryRemove(Key(args.ContainerName, args.BlobName), out _));
    }

    public override Task<bool> ExistsAsync(BlobProviderExistsArgs args)
    {
        return Task.FromResult(Blobs.ContainsKey(Key(args.ContainerName, args.BlobName)));
    }

    public override Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
    {
        return Task.FromResult<Stream?>(
            Blobs.TryGetValue(Key(args.ContainerName, args.BlobName), out var bytes) ? new MemoryStream(bytes) : null);
    }

    private static string Key(string containerName, string blobName) => containerName + "/" + blobName;
}

public class FixedBlobNameGenerator : IBlobNameGenerator, ITransientDependency
{
    public const string BlobName = "fixed-blob";

    public Task<string> Create() => Task.FromResult(BlobName);
}

public class HandlerLog : ISingletonDependency
{
    public ConcurrentQueue<string> Entries { get; } = new();
}

public class RecordingHandler : IFileHandler, ITransientDependency
{
    private readonly HandlerLog _log;

    public RecordingHandler(HandlerLog log) => _log = log;

    public Task ExecuteAsync(FileHandlerContext context)
    {
        _log.Entries.Enqueue($"first:{context.MimeType}");
        return Task.CompletedTask;
    }
}

public class UppercaseHandler : IFileHandler, ITransientDependency
{
    private readonly HandlerLog _log;

    public UppercaseHandler(HandlerLog log) => _log = log;

    public async Task ExecuteAsync(FileHandlerContext context)
    {
        _log.Entries.Enqueue("uppercase");
        using var reader = new StreamReader(context.BlobStream, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(context.CancellationToken);
        context.BlobStream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToUpperInvariant()));
    }
}

public class SecondRecordingHandler : IFileHandler, ITransientDependency
{
    private readonly HandlerLog _log;

    public SecondRecordingHandler(HandlerLog log) => _log = log;

    public async Task ExecuteAsync(FileHandlerContext context)
    {
        using var reader = new StreamReader(context.BlobStream, Encoding.UTF8, leaveOpen: true);
        _log.Entries.Enqueue($"second:{await reader.ReadToEndAsync(context.CancellationToken)}");
    }
}

/// <summary>
/// Wraps a stream, counting the bytes read from it; can hide seeking to look like a network stream.
/// </summary>
public class TrackingStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _canSeek;

    public TrackingStream(byte[] content, bool canSeek)
    {
        _inner = new MemoryStream(content);
        _canSeek = canSeek;
    }

    public long BytesRead { get; private set; }

    public bool IsDisposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => _canSeek;

    public override bool CanWrite => false;

    public override long Length => _canSeek ? _inner.Length : throw new NotSupportedException();

    public override long Position
    {
        get => _canSeek ? _inner.Position : throw new NotSupportedException();
        set
        {
            if (!_canSeek)
            {
                throw new NotSupportedException();
            }

            _inner.Position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        _canSeek ? _inner.Seek(offset, origin) : throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

public static class Samples
{
    public static byte[] Png { get; } = Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

    public static byte[] Zip { get; } = Pad([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]);

    public static byte[] Pdf { get; } = Pad(Encoding.ASCII.GetBytes("%PDF-1.7\n"));

    public static byte[] Text { get; } = Encoding.UTF8.GetBytes("hello world, plain text");

    /// <summary>
    /// Text that happens to start with "MZ" — must not be mistaken for an executable.
    /// </summary>
    public static byte[] TextStartingWithMz { get; } =
        Encoding.UTF8.GetBytes("MZ is how this note starts, and it keeps going long enough to cover the probe.");

    public static byte[] Executable { get; } = CreateExecutable();

    public static byte[] UnknownBinary { get; } = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    public static byte[] Get(string name) => name switch
    {
        nameof(Png) => Png,
        nameof(Zip) => Zip,
        nameof(Pdf) => Pdf,
        nameof(Text) => Text,
        nameof(TextStartingWithMz) => TextStartingWithMz,
        nameof(Executable) => Executable,
        nameof(UnknownBinary) => UnknownBinary,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };

    private static byte[] CreateExecutable()
    {
        var bytes = new byte[0x100];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3C] = 0x80; // e_lfanew
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        return bytes;
    }

    private static byte[] Pad(IReadOnlyCollection<byte> header)
    {
        var bytes = new byte[96];
        var index = 0;
        foreach (var value in header)
        {
            bytes[index++] = value;
        }

        return bytes;
    }
}
