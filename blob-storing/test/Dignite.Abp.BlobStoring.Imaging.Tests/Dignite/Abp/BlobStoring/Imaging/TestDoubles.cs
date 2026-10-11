using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Wraps content, recording whether it was disposed; can hide seeking to look like a network stream.
/// </summary>
public class TrackingStream : Stream
{
    private readonly MemoryStream _inner;
    private readonly bool _canSeek;

    public TrackingStream(byte[] content, bool canSeek)
    {
        _inner = new MemoryStream(content);
        _canSeek = canSeek;
    }

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

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);

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

public delegate Task<(Stream Result, ImageProcessState State)> FakeImageOperation(
    Stream stream,
    string? mimeType,
    CancellationToken cancellationToken);

/// <summary>
/// An <see cref="IImageResizer"/> whose behaviour each test scripts with <see cref="Operation"/>. By default it fails
/// the test if it is called at all.
/// </summary>
public class FakeImageResizer : IImageResizer
{
    public FakeImageOperation Operation { get; set; } =
        (_, _, _) => throw new InvalidOperationException("The image resizer must not be called.");

    public int CallCount { get; private set; }

    public ImageResizeArgs? LastArgs { get; private set; }

    public async Task<ImageResizeResult<Stream>> ResizeAsync(
        Stream stream,
        ImageResizeArgs resizeArgs,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastArgs = resizeArgs;
        var (result, state) = await Operation(stream, mimeType, cancellationToken);
        return new ImageResizeResult<Stream>(result, state);
    }

    public Task<ImageResizeResult<byte[]>> ResizeAsync(
        byte[] bytes,
        ImageResizeArgs resizeArgs,
        string? mimeType = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>
/// An <see cref="IImageCompressor"/> whose behaviour each test scripts with <see cref="Operation"/>. By default it
/// fails the test if it is called at all.
/// </summary>
public class FakeImageCompressor : IImageCompressor
{
    public FakeImageOperation Operation { get; set; } =
        (_, _, _) => throw new InvalidOperationException("The image compressor must not be called.");

    public int CallCount { get; private set; }

    public async Task<ImageCompressResult<Stream>> CompressAsync(
        Stream stream,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        var (result, state) = await Operation(stream, mimeType, cancellationToken);
        return new ImageCompressResult<Stream>(result, state);
    }

    public Task<ImageCompressResult<byte[]>> CompressAsync(
        byte[] bytes,
        string? mimeType = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
