using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.BlobStoring.Pipeline;

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

    public static byte[] Jpeg { get; } = Pad([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00]);

    /// <summary>
    /// A ZIP local file header only: not a readable archive.
    /// </summary>
    public static byte[] ZipHeader { get; } = Pad([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]);

    public static byte[] Pdf { get; } = Pad(Encoding.ASCII.GetBytes("%PDF-1.7\n"));

    public static byte[] Wav { get; } = Pad(Encoding.ASCII.GetBytes("RIFF$\0\0\0WAVEfmt "));

    public static byte[] Text { get; } = Encoding.UTF8.GetBytes("hello world, plain text");

    /// <summary>
    /// Text that happens to start with "MZ" — must not be mistaken for an executable.
    /// </summary>
    public static byte[] TextStartingWithMz { get; } =
        Encoding.UTF8.GetBytes("MZ is how this note starts, and it keeps going long enough to cover the probe.");

    /// <summary>
    /// Text that happens to start with "BM" — must not be mistaken for a bitmap.
    /// </summary>
    public static byte[] TextStartingWithBm { get; } =
        Encoding.UTF8.GetBytes("BMW service notes: replace the brake pads, check the tyres and the oil level.");

    /// <summary>
    /// Text that happens to start with "ID3" — must not be mistaken for an MP3.
    /// </summary>
    public static byte[] TextStartingWithId3 { get; } =
        Encoding.UTF8.GetBytes("ID3 tags carry the title and artist of a song; this note is about them.");

    /// <summary>
    /// Latin-1 text: not valid UTF-8, still text.
    /// </summary>
    public static byte[] Latin1Text { get; } = Encoding.Latin1.GetBytes("name;city\nRené;Zürich\nJosé;São Paulo\n");

    public static byte[] Utf16Text { get; } = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("hello, UTF-16 text")];

    public static byte[] Html { get; } =
        Encoding.UTF8.GetBytes("﻿  <!doctype html>\n<html><body><script>alert(1)</script></body></html>");

    public static byte[] Script { get; } = Encoding.UTF8.GetBytes("<script>alert(document.cookie)</script>");

    public static byte[] Svg { get; } =
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"></svg>");

    public static byte[] SvgWithProlog { get; } = Encoding.UTF8.GetBytes(
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- Generator: test -->\n" +
        "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n" +
        "<svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\"></svg:svg>");

    public static byte[] Xml { get; } = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\n<note><to>you</to></note>");

    public static byte[] MarkdownWithComment { get; } =
        Encoding.UTF8.GetBytes("<!-- generated -->\n# Title\n\nSome *markdown*.\n");

    public static byte[] Executable { get; } = CreateExecutable();

    public static byte[] UnknownBinary { get; } = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    public static byte[] WordDocument { get; } = CreateZip(
        ("[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/word/document.xml\" " +
            "ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
            "</Types>"),
        ("word/document.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body><w:p><w:r><w:t>Hello</w:t></w:r></w:p></w:body></w:document>"));

    public static byte[] PlainZip { get; } = CreateZip(("readme.txt", "just a plain archive"));

    public static byte[] Get(string name) => name switch
    {
        nameof(Png) => Png,
        nameof(Jpeg) => Jpeg,
        nameof(ZipHeader) => ZipHeader,
        nameof(Pdf) => Pdf,
        nameof(Wav) => Wav,
        nameof(Text) => Text,
        nameof(TextStartingWithMz) => TextStartingWithMz,
        nameof(TextStartingWithBm) => TextStartingWithBm,
        nameof(TextStartingWithId3) => TextStartingWithId3,
        nameof(Latin1Text) => Latin1Text,
        nameof(Utf16Text) => Utf16Text,
        nameof(Html) => Html,
        nameof(Script) => Script,
        nameof(Svg) => Svg,
        nameof(SvgWithProlog) => SvgWithProlog,
        nameof(Xml) => Xml,
        nameof(MarkdownWithComment) => MarkdownWithComment,
        nameof(Executable) => Executable,
        nameof(UnknownBinary) => UnknownBinary,
        nameof(WordDocument) => WordDocument,
        nameof(PlainZip) => PlainZip,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };

    public static byte[] Gzip(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(content);
        }

        return output.ToArray();
    }

    public static byte[] Gunzip(byte[] content)
    {
        using var gzip = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entryStream = archive.CreateEntry(name).Open();
                entryStream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return output.ToArray();
    }

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
