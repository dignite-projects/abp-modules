using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

public class MimeTypeDetector_Tests : AbpIntegratedTest<BlobStoringPipelineTestModule>
{
    private const string WordDocumentType = BlobStoringPipelineTestModule.WordDocumentType;

    private readonly IMimeTypeDetector _detector;

    public MimeTypeDetector_Tests()
    {
        _detector = GetRequiredService<IMimeTypeDetector>();
    }

    [Theory]
    [InlineData(nameof(Samples.Png), "photo.png", "image/png")]
    [InlineData(nameof(Samples.Png), "photo.jpg", "image/png")] // same kind: the content wins
    [InlineData(nameof(Samples.Png), "photo", "image/png")]
    [InlineData(nameof(Samples.Png), null, "image/png")]
    [InlineData(nameof(Samples.Png), "photo.unknown-extension", "image/png")]
    [InlineData(nameof(Samples.Png), "tenants/42/avatars/photo.png", "image/png")] // path-like BLOB name
    [InlineData(nameof(Samples.Jpeg), "photo.jpg", "image/jpeg")]
    [InlineData(nameof(Samples.Text), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.Text), "data.csv", "text/csv")]
    [InlineData(nameof(Samples.Text), "notes", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithMz), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithBm), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithId3), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.Latin1Text), "export.csv", "text/csv")]
    [InlineData(nameof(Samples.Utf16Text), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.ZipHeader), "report.docx", WordDocumentType)] // generic container: the extension names the format
    [InlineData(nameof(Samples.ZipHeader), "archive.zip", "application/zip")]
    [InlineData(nameof(Samples.ZipHeader), "archive", "application/zip")]
    [InlineData(nameof(Samples.Pdf), "Report.PDF", "application/pdf")]
    [InlineData(nameof(Samples.Wav), "sound.wav", "audio/wav")] // not a FileSignatures format: the header probe finds it
    [InlineData(nameof(Samples.UnknownBinary), "data.bin", MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(Samples.UnknownBinary), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(Samples.UnknownBinary), "notes.txt", MimeTypeDetector.DefaultMimeType)] // binary is not text
    [InlineData(nameof(Samples.UnknownBinary), "movie.mp4", "video/mp4")] // MP4 has no guaranteed leading signature
    public async Task DetectAsync_Should_Decide_From_The_Content(string sample, string? fileName, string expected)
    {
        var stream = new MemoryStream(Samples.Get(sample));

        (await _detector.DetectAsync(stream, fileName)).ShouldBe(expected);
        stream.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(Samples.WordDocument), "report.docx", WordDocumentType)]
    [InlineData(nameof(Samples.WordDocument), null, WordDocumentType)]
    [InlineData(nameof(Samples.WordDocument), "report.zip", WordDocumentType)] // same kind, not generic: the content wins
    [InlineData(nameof(Samples.PlainZip), "archive.zip", "application/zip")]
    [InlineData(nameof(Samples.PlainZip), null, "application/zip")]
    public async Task DetectAsync_Should_Tell_Office_Documents_From_Plain_Zip_Archives(string sample, string? fileName, string expected)
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Get(sample)), fileName)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(nameof(Samples.Html), null, "text/html")]
    [InlineData(nameof(Samples.Html), "page.html", "text/html")]
    [InlineData(nameof(Samples.Html), "notes.txt", "text/html")] // text kind: the sniffed markup wins
    [InlineData(nameof(Samples.Script), "notes.txt", "text/html")]
    [InlineData(nameof(Samples.Svg), null, "image/svg+xml")]
    [InlineData(nameof(Samples.Svg), "icon.svg", "image/svg+xml")]
    [InlineData(nameof(Samples.Svg), "notes.txt", "image/svg+xml")]
    [InlineData(nameof(Samples.SvgWithProlog), "icon.svg", "image/svg+xml")]
    [InlineData(nameof(Samples.SvgWithProlog), "data.xml", "image/svg+xml")]
    [InlineData(nameof(Samples.Xml), null, "application/xml")]
    [InlineData(nameof(Samples.Xml), "notes.txt", "application/xml")]
    [InlineData(nameof(Samples.Xml), "icon.svg", "application/xml")] // XML, but not an SVG document
    [InlineData(nameof(Samples.MarkdownWithComment), "readme.md", "text/markdown")]
    [InlineData(nameof(Samples.Text), "icon.svg", "image/svg+xml")] // no markup: the extension's text type
    [InlineData(nameof(Samples.Text), "data.json", "application/json")]
    public async Task DetectAsync_Should_Sniff_Text_Without_A_Signature(string sample, string? fileName, string expected)
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Get(sample)), fileName)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(nameof(Samples.Text), "photo.png")]          // no PNG signature
    [InlineData(nameof(Samples.Text), "report.pdf")]         // no PDF signature
    [InlineData(nameof(Samples.Text), "song.mp3")]           // text is not audio
    [InlineData(nameof(Samples.Svg), "photo.png")]           // SVG is not a raster image
    [InlineData(nameof(Samples.Png), "icon.svg")]            // a raster image is not SVG
    [InlineData(nameof(Samples.Executable), "photo.png")]    // executable disguised as an image
    [InlineData(nameof(Samples.Executable), "readme.txt")]   // executable disguised as text
    [InlineData(nameof(Samples.Executable), "report.pdf")]
    [InlineData(nameof(Samples.ZipHeader), "report.pdf")]    // archive disguised as a document
    [InlineData(nameof(Samples.WordDocument), "report.pdf")]
    [InlineData(nameof(Samples.Png), "notes.txt")]           // binary where text is claimed
    [InlineData(nameof(Samples.UnknownBinary), "photo.jpg")] // the format always has a signature
    public async Task DetectAsync_Should_Reject_A_Disguised_File(string sample, string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _detector.DetectAsync(new MemoryStream(Samples.Get(sample)), fileName));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        exception.Data["Extension"].ShouldBe(Path.GetExtension(fileName));
    }

    [Fact]
    public async Task DetectAsync_Should_Detect_An_Executable_Without_Extension()
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Executable), "setup"))
            .ShouldBe("application/x-msdownload");
    }

    [Fact]
    public async Task DetectAsync_Should_Detect_Gzip_Content()
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Gzip(Samples.Text)), "notes.txt.gz"))
            .ShouldBe("application/gzip");
    }

    [Fact]
    public async Task DetectAsync_Should_Return_Octet_Stream_For_An_Empty_File_Without_Extension()
    {
        (await _detector.DetectAsync(new MemoryStream(), "empty")).ShouldBe(MimeTypeDetector.DefaultMimeType);
    }

    [Fact]
    public async Task DetectAsync_Should_Reject_An_Empty_File_Whose_Format_Has_A_Signature()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => _detector.DetectAsync(new MemoryStream(), "photo.png"));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
    }

    [Fact]
    public async Task DetectAsync_Should_Probe_From_The_Start_And_Rewind()
    {
        var stream = new MemoryStream(Samples.Png) { Position = 10 };

        (await _detector.DetectAsync(stream, "photo.png")).ShouldBe("image/png");
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_Should_Require_A_Seekable_Stream()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _detector.DetectAsync(new TrackingStream(Samples.Png, canSeek: false), "photo.png"));
    }
}
