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
    [InlineData(nameof(FontSamples.Woff), "font.woff", "font/woff")]
    [InlineData(nameof(FontSamples.Woff2), "font.woff2", "font/woff2")]
    [InlineData(nameof(FontSamples.TrueType), "font.ttf", "font/ttf")]
    [InlineData(nameof(FontSamples.AppleTrueType), "font.ttf", "font/ttf")] // the "true" sfnt version is TrueType too
    [InlineData(nameof(FontSamples.OpenType), "font.otf", "font/otf")]
    [InlineData(nameof(FontSamples.Woff2), "Inter-Regular.WOFF2", "font/woff2")]
    [InlineData(nameof(FontSamples.Woff2), "themes/42/fonts/inter.woff2", "font/woff2")] // path-like BLOB name
    [InlineData(nameof(FontSamples.Woff), null, "font/woff")]
    [InlineData(nameof(FontSamples.Woff2), "font", "font/woff2")]
    [InlineData(nameof(FontSamples.TrueType), "font.dat", "font/ttf")]
    [InlineData(nameof(FontSamples.OpenType), null, "font/otf")]
    [InlineData(nameof(FontSamples.TrueType), "font.otf", "font/ttf")] // same kind: the content wins
    [InlineData(nameof(FontSamples.OpenType), "font.ttf", "font/otf")]
    [InlineData(nameof(FontSamples.Woff2), "font.woff", "font/woff2")]
    [InlineData(nameof(FontSamples.Woff), "font.ttf", "font/woff")]
    [InlineData(nameof(FontSamples.WoffWithAppleTrueTypeFlavor), "font.woff", "font/woff")]
    [InlineData(nameof(FontSamples.Woff2WithCollectionFlavor), "font.woff2", "font/woff2")]
    [InlineData(nameof(FontSamples.TrueTypeWithManyTables), "font.ttf", "font/ttf")] // its table directory runs past the probed bytes
    public async Task DetectAsync_Should_Detect_Web_Fonts_From_Their_Bytes(string sample, string? fileName, string expected)
    {
        var stream = new MemoryStream(Samples.Get(sample));

        (await _detector.DetectAsync(stream, fileName)).ShouldBe(expected);
        stream.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(Samples.TextStartingWithWoff), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithWoff2), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithTrue), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithOtto), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithOtto), null, "text/plain")]
    // The smallest table counts text can produce: only the cap on the table count tells it from an sfnt font.
    [InlineData(nameof(Samples.TextStartingWithTrueAndTabs), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithTrueAndNewlines), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithOttoAndLineBreak), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.TextStartingWithTrueAndNewlines), null, "text/plain")]
    [InlineData(nameof(Samples.RandomBinary), "data.bin", MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(Samples.RandomBinary), null, MimeTypeDetector.DefaultMimeType)]
    // A font signature that the rest of the header does not bear out is not a font.
    [InlineData(nameof(FontSamples.TrueTypeWithoutTables), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.TrueTypeWithTooManyTables), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.TruncatedTrueType), null, MimeTypeDetector.DefaultMimeType)] // no whole table record
    [InlineData(nameof(FontSamples.TrueTypeWithBinaryTableTag), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.TrueTypeWithTableInsideDirectory), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.TrueTypeWithTableBeyond32Bits), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffWithoutTables), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffWithTooManyTables), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.Woff2WithoutTables), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffWithUnknownFlavor), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffWithReservedFieldSet), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffShorterThanItsHeader), null, MimeTypeDetector.DefaultMimeType)]
    [InlineData(nameof(FontSamples.WoffTruncated), null, MimeTypeDetector.DefaultMimeType)] // shorter than the header
    [InlineData(nameof(FontSamples.Woff2Truncated), null, MimeTypeDetector.DefaultMimeType)]
    public async Task DetectAsync_Should_Not_Take_Other_Content_For_A_Font(string sample, string? fileName, string expected)
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Get(sample)), fileName)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("00010000")] // TrueType
    [InlineData("74727565")] // "true"
    [InlineData("4F54544F")] // "OTTO"
    [InlineData("774F4646")] // "wOFF"
    [InlineData("774F4632")] // "wOF2"
    public async Task DetectAsync_Should_Not_Take_Random_Bytes_Behind_A_Font_Signature_For_A_Font(string signatureHex)
    {
        var signature = Convert.FromHexString(signatureHex);

        for (var seed = 0; seed < 200; seed++)
        {
            var content = Samples.CreateRandomBinary(seed, signature);

            var mimeType = await _detector.DetectAsync(new MemoryStream(content), null);

            mimeType.StartsWith("font/", StringComparison.Ordinal).ShouldBeFalse($"seed {seed} was detected as {mimeType}");
        }
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
    [InlineData(nameof(Samples.Executable), "font.woff2")]   // executable disguised as a font
    [InlineData(nameof(Samples.Executable), "font.ttf")]
    [InlineData(nameof(Samples.ZipHeader), "font.otf")]
    [InlineData(nameof(Samples.Png), "font.woff")]
    [InlineData(nameof(FontSamples.Woff2), "photo.png")]     // font disguised as an image
    [InlineData(nameof(FontSamples.TrueType), "photo.png")]
    [InlineData(nameof(FontSamples.OpenType), "report.pdf")]
    [InlineData(nameof(FontSamples.Woff), "notes.txt")]      // binary where text is claimed
    [InlineData(nameof(FontSamples.Woff), "setup.exe")]
    [InlineData(nameof(Samples.Text), "font.ttf")]           // text is not a font
    [InlineData(nameof(Samples.Text), "font.woff2")]
    [InlineData(nameof(Samples.Svg), "font.woff")]
    [InlineData(nameof(Samples.TextStartingWithWoff), "font.woff")]
    [InlineData(nameof(Samples.TextStartingWithTrue), "font.ttf")]
    [InlineData(nameof(Samples.TextStartingWithOtto), "font.otf")]
    [InlineData(nameof(Samples.UnknownBinary), "font.otf")]  // the format always has a signature
    [InlineData(nameof(Samples.RandomBinary), "font.ttf")]
    [InlineData(nameof(Samples.TextStartingWithTrueAndNewlines), "font.ttf")]
    [InlineData(nameof(FontSamples.TrueTypeWithoutTables), "font.ttf")]
    [InlineData(nameof(FontSamples.TrueTypeWithTableInsideDirectory), "font.ttf")]
    [InlineData(nameof(FontSamples.WoffTruncated), "font.woff")]
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

    [Theory]
    [InlineData("photo.png")]
    [InlineData("font.woff2")]
    public async Task DetectAsync_Should_Reject_An_Empty_File_Whose_Format_Has_A_Signature(string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => _detector.DetectAsync(new MemoryStream(), fileName));

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
