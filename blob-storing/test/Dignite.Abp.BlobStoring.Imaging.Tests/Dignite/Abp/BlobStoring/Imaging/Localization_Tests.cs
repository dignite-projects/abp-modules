using Dignite.Abp.BlobStoring.Imaging.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class Localization_Tests : AbpIntegratedTest<BlobStoringImagingTestModule>
{
    [Theory]
    [InlineData("en", BlobStoringImagingErrorCodes.ImageTooLarge)]
    [InlineData("en", BlobStoringImagingErrorCodes.ImageTooSmall)]
    [InlineData("en", BlobStoringImagingErrorCodes.ImageDecodeTimeout)]
    [InlineData("en", BlobStoringImagingErrorCodes.ImageProcessingFailed)]
    [InlineData("zh-Hans", BlobStoringImagingErrorCodes.ImageTooLarge)]
    [InlineData("zh-Hans", BlobStoringImagingErrorCodes.ImageTooSmall)]
    [InlineData("zh-Hans", BlobStoringImagingErrorCodes.ImageDecodeTimeout)]
    [InlineData("zh-Hans", BlobStoringImagingErrorCodes.ImageProcessingFailed)]
    [InlineData("zh-Hant", BlobStoringImagingErrorCodes.ImageTooLarge)]
    [InlineData("zh-Hant", BlobStoringImagingErrorCodes.ImageProcessingFailed)]
    [InlineData("ja", BlobStoringImagingErrorCodes.ImageTooLarge)]
    [InlineData("ja", BlobStoringImagingErrorCodes.ImageProcessingFailed)]
    public void Every_Error_Code_Should_Be_Localized(string culture, string code)
    {
        using (CultureHelper.Use(culture))
        {
            var text = GetRequiredService<IStringLocalizer<BlobStoringImagingResource>>()[code];

            text.ResourceNotFound.ShouldBeFalse();
            text.Value.ShouldNotBe(code);
        }
    }
}
