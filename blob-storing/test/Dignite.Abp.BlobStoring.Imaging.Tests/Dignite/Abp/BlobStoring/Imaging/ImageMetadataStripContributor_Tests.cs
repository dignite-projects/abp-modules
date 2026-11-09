using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class ImageMetadataStripContributor_Tests : BlobStoringImagingTestBase
{
    private const string ContainerName = BlobStoringImagingTestModule.MetadataStripContainer;

    private static readonly byte[] Exif = JpegSegment(0xE1, Concat("Exif\0\0"u8.ToArray(), new byte[]
    {
        0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x00, 0x01,
        0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x06, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    }));

    private static readonly byte[] MainXmp = JpegSegment(0xE1, Concat("http://ns.adobe.com/xap/1.0/\0"u8.ToArray(),
        "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><xmpNote:HasExtendedXMP>0123</xmpNote:HasExtendedXMP></x:xmpmeta>"u8.ToArray()));

    private static readonly byte[] Icc = JpegSegment(0xE2, Concat("ICC_PROFILE\0\x01\x01"u8.ToArray(), new byte[512]));

    private static readonly byte[] Adobe = JpegSegment(0xEE, "Adobe\0\x64\0\0\0\0\x01"u8.ToArray());

    /// <summary>
    /// A JPEG as Photoshop exports one: megabytes of Extended XMP, the Photoshop image resources and a comment go;
    /// EXIF, the main XMP packet, the ICC profile, the Adobe segment and the image stay byte for byte, so what is
    /// stored is the content less those segments. The caller's stream stays open.
    /// </summary>
    [Fact]
    public async Task Should_Drop_Extended_Xmp_Photoshop_Resources_And_Comments_From_A_Jpeg()
    {
        var jpeg = TestImages.Noise(320, 240, SKEncodedImageFormat.Jpeg, quality: 90);
        var ancestors = Encoding.ASCII.GetBytes(string.Concat(
            Enumerable.Repeat("<rdf:li>0000A6C7815905497C2762FB3073AC1B</rdf:li> ", 1300)))[..65000];
        var segments = new List<byte[]> { Exif, MainXmp, Icc };
        for (var i = 0; i < 20; i++)
        {
            segments.Add(JpegSegment(0xE1, Concat("http://ns.adobe.com/xmp/extension/\0"u8.ToArray(), ancestors)));
        }

        segments.Add(JpegSegment(0xED, Concat("Photoshop 3.0\08BIM"u8.ToArray(), new byte[4000])));
        segments.Add(JpegSegment(0xFE, "Saved by a design tool"u8.ToArray()));
        segments.Add(Adobe);
        var content = Concat(jpeg[..2], Concat(segments.ToArray()), jpeg[2..]);
        var source = new TrackingStream(content, canSeek: false);

        await Container(ContainerName).SaveAsync("banner.jpg", source);

        var stored = await GetStoredBytesAsync(ContainerName, "banner.jpg");
        stored.ShouldBe(Concat(jpeg[..2], Exif, MainXmp, Icc, Adobe, jpeg[2..]));
        (await ReadAllAsync(await Container(ContainerName).GetAsync("banner.jpg"))).ShouldBe(stored);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Drop_The_Text_Chunks_And_Time_Of_A_Png()
    {
        var png = TestImages.Noise(64, 48, SKEncodedImageFormat.Png);
        var ihdrEnd = 8 + 12 + 13;
        var content = Concat(
            png[..ihdrEnd],
            PngChunk("tEXt", "Software\0Design Tool"u8.ToArray()),
            PngChunk("iTXt", Concat("XML:com.adobe.xmp\0\0\0\0\0"u8.ToArray(), new byte[50_000])),
            PngChunk("zTXt", "Comment\0\0\x78\x9c\x03\0\0\0\0\x01"u8.ToArray()),
            PngChunk("tIME", new byte[] { 0x07, 0xEA, 11, 9, 12, 0, 0 }),
            png[ihdrEnd..]);

        await Container(ContainerName).SaveAsync("drawing.png", content);

        (await GetStoredBytesAsync(ContainerName, "drawing.png")).ShouldBe(png);
    }

    /// <summary>
    /// An image with nothing to remove, content that is not an image, and a JPEG whose segments do not parse are
    /// stored as they were given.
    /// </summary>
    [Theory]
    [InlineData("plain.jpg")]
    [InlineData("plain.png")]
    [InlineData("notes.txt")]
    [InlineData("truncated.jpg")]
    public async Task Should_Store_Content_With_Nothing_To_Remove_As_It_Is(string blobName)
    {
        var content = blobName switch
        {
            "plain.jpg" => Concat(TestImages.Noise(64, 48, SKEncodedImageFormat.Jpeg)[..2], Exif, Adobe,
                TestImages.Noise(64, 48, SKEncodedImageFormat.Jpeg)[2..]),
            "plain.png" => TestImages.Noise(64, 48, SKEncodedImageFormat.Png),
            "notes.txt" => Encoding.UTF8.GetBytes("plain text is not an image"),
            // A comment segment declaring more bytes than the file has: not a JPEG this walks, so nothing is dropped.
            _ => Concat(new byte[] { 0xFF, 0xD8 }, new byte[] { 0xFF, 0xFE, 0x40, 0x00 }, "short"u8.ToArray())
        };

        await Container(ContainerName).SaveAsync(blobName, content);

        (await GetStoredBytesAsync(ContainerName, blobName)).ShouldBe(content);
    }

    /// <summary>
    /// Before the resize contributor, the decode guard and the resize see the image without the metadata: an image
    /// that fits is stored less its metadata, one that does not is resized.
    /// </summary>
    [Fact]
    public async Task Should_Run_Before_The_Resize_Contributor()
    {
        const string container = BlobStoringImagingTestModule.MetadataStripThenResizeContainer;
        var small = TestImages.Noise(100, 80, SKEncodedImageFormat.Jpeg, quality: 90);
        var large = TestImages.Noise(600, 400, SKEncodedImageFormat.Jpeg, quality: 90);
        var comment = JpegSegment(0xFE, new byte[30_000]);

        await Container(container).SaveAsync("small.jpg", Concat(small[..2], comment, small[2..]));
        await Container(container).SaveAsync("large.jpg", Concat(large[..2], comment, large[2..]));

        (await GetStoredBytesAsync(container, "small.jpg")).ShouldBe(small);
        var resized = await GetStoredBytesAsync(container, "large.jpg");
        resized.ShouldNotBeNull();
        TestImages.GetDimensions(resized).ShouldBe((BlobStoringImagingTestModule.ComposedMaxSize, 171));
    }

    [Fact]
    public void AddImageMetadataStripContributor_Should_Add_The_Contributor_Once()
    {
        var configuration = new BlobContainerConfiguration();

        configuration.AddImageMetadataStripContributor();
        configuration.AddImageMetadataStripContributor(_ => { });

        configuration.PipelineContributors.ShouldBe([typeof(ImageMetadataStripContributor)]);
    }

    private static byte[] JpegSegment(byte marker, byte[] body)
    {
        var length = body.Length + 2;
        return Concat(new byte[] { 0xFF, marker, (byte)(length >> 8), (byte)length }, body);
    }

    private static byte[] PngChunk(string type, byte[] data)
    {
        var typeAndData = Concat(Encoding.ASCII.GetBytes(type), data);
        var crc = 0xFFFFFFFFu;
        foreach (var b in typeAndData)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        crc = ~crc;
        return Concat(
            new[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length },
            typeAndData,
            new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
