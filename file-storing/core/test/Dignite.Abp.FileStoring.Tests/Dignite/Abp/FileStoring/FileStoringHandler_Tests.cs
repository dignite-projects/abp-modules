using System.IO;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.FileStoring;

public class FileStoringHandler_Tests
{
    [Fact]
    public async Task FileSizeLimitHandler_Should_Reject_Too_Large_Stream()
    {
        var configuration = CreateConfiguration();
        configuration.AddFileSizeLimitHandler(options => options.MaxFileSize = 1);
        var stream = new MemoryStream(new byte[(1 * 1024 * 1024) + 1]);
        var context = new FileHandlerContext("avatar.png", "image/png", stream, configuration);

        await Should.ThrowAsync<BusinessException>(() => new FileSizeLimitHandler().ExecuteAsync(context));
    }

    [Fact]
    public void FileSizeLimitHandler_Should_Reject_Invalid_Configuration()
    {
        var configuration = CreateConfiguration();

        Should.Throw<System.ArgumentOutOfRangeException>(() =>
            configuration.AddFileSizeLimitHandler(options => options.MaxFileSize = 0));
    }

    [Fact]
    public async Task FileTypeCheckHandler_Should_Reject_Not_Allowed_Extension()
    {
        var configuration = CreateConfiguration();
        configuration.AddFileTypeCheckHandler(options => options.AllowedFileTypeNames = new[] { ".png" });
        var context = new FileHandlerContext("avatar.exe", "application/octet-stream", new MemoryStream(), configuration);

        await Should.ThrowAsync<BusinessException>(() => new FileTypeCheckHandler().ExecuteAsync(context));
    }

    [Fact]
    public void Md5_Should_Reset_Stream_Position()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        stream.Position = 0;

        var md5 = stream.Md5();

        md5.ShouldBe("900150983CD24FB0D6963F7D28E17F72");
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void Sha256_Should_Reset_Stream_Position()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        stream.Position = 0;

        var sha256 = stream.Sha256();

        sha256.ShouldBe("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
        stream.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/webp", true)]
    [InlineData("application/pdf", false)]
    public void ImageFormatHelper_Should_Check_Mime_Type(string mimeType, bool expected)
    {
        ImageFormatHelper.IsValidImage(mimeType, ImageFormatHelper.AllowedImageUploadFormats)
            .ShouldBe(expected);
    }

    private static BlobContainerConfiguration CreateConfiguration()
    {
        return new BlobContainerConfiguration();
    }
}
