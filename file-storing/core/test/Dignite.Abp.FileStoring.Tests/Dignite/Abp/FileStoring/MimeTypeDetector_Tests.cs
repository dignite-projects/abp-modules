using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.FileStoring;

public class MimeTypeDetector_Tests
{
    private readonly MimeTypeDetector _detector = new();

    [Theory]
    [InlineData(nameof(Samples.Png), "photo.png", "image/png")]
    [InlineData(nameof(Samples.Png), "photo.jpg", "image/png")] // same kind: the content wins
    [InlineData(nameof(Samples.Png), "photo", "image/png")]
    [InlineData(nameof(Samples.Png), "photo.unknown-extension", "image/png")]
    [InlineData(nameof(Samples.Text), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.Text), "data.csv", "text/csv")]
    [InlineData(nameof(Samples.TextStartingWithMz), "notes.txt", "text/plain")]
    [InlineData(nameof(Samples.Zip), "report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(nameof(Samples.Zip), "archive.zip", "application/zip")]
    [InlineData(nameof(Samples.Zip), "archive", "application/zip")]
    [InlineData(nameof(Samples.Pdf), "Report.PDF", "application/pdf")]
    [InlineData(nameof(Samples.UnknownBinary), "data.bin", MimeTypeDetector.DefaultMimeType)]
    public async Task DetectAsync_Should_Decide_From_The_Content(string sample, string fileName, string expected)
    {
        var stream = new MemoryStream(Samples.Get(sample));

        (await _detector.DetectAsync(stream, fileName)).ShouldBe(expected);
        stream.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(Samples.Text), "photo.png")]        // no PNG signature
    [InlineData(nameof(Samples.Text), "report.pdf")]       // no PDF signature
    [InlineData(nameof(Samples.Executable), "photo.png")]  // executable disguised as an image
    [InlineData(nameof(Samples.Executable), "readme.txt")] // executable disguised as text
    [InlineData(nameof(Samples.Zip), "report.pdf")]        // archive disguised as a document
    [InlineData(nameof(Samples.Png), "notes.txt")]         // binary where text is claimed
    [InlineData(nameof(Samples.UnknownBinary), "photo.jpg")]
    public async Task DetectAsync_Should_Reject_A_Disguised_File(string sample, string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _detector.DetectAsync(new MemoryStream(Samples.Get(sample)), fileName));

        exception.Code.ShouldBe(FileErrorCodes.Files.ContentTypeMismatch);
    }

    [Fact]
    public async Task DetectAsync_Should_Detect_An_Executable_Without_Extension()
    {
        (await _detector.DetectAsync(new MemoryStream(Samples.Executable), "setup"))
            .ShouldBe("application/x-msdownload");
    }

    [Fact]
    public async Task DetectAsync_Should_Return_Octet_Stream_For_An_Empty_File_Without_Extension()
    {
        (await _detector.DetectAsync(new MemoryStream(), "empty")).ShouldBe(MimeTypeDetector.DefaultMimeType);
    }

    [Fact]
    public async Task DetectAsync_Should_Require_A_Seekable_Stream()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _detector.DetectAsync(new TrackingStream(Samples.Png, canSeek: false), "photo.png"));
    }
}
