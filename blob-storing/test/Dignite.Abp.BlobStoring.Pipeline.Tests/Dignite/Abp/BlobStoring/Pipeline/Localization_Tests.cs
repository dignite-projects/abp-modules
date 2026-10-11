using Dignite.Abp.BlobStoring.Pipeline.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

public class Localization_Tests : AbpIntegratedTest<BlobStoringPipelineTestModule>
{
    [Theory]
    [InlineData("en", BlobStoringPipelineErrorCodes.ContentTooLarge)]
    [InlineData("en", BlobStoringPipelineErrorCodes.ContentTypeMismatch)]
    [InlineData("en", BlobStoringPipelineErrorCodes.ContentTypeNotAllowed)]
    [InlineData("zh-Hans", BlobStoringPipelineErrorCodes.ContentTooLarge)]
    [InlineData("zh-Hans", BlobStoringPipelineErrorCodes.ContentTypeMismatch)]
    [InlineData("zh-Hans", BlobStoringPipelineErrorCodes.ContentTypeNotAllowed)]
    [InlineData("zh-Hant", BlobStoringPipelineErrorCodes.ContentTypeNotAllowed)]
    [InlineData("ja", BlobStoringPipelineErrorCodes.ContentTypeNotAllowed)]
    public void Every_Error_Code_Should_Be_Localized(string culture, string code)
    {
        using (CultureHelper.Use(culture))
        {
            var text = GetRequiredService<IStringLocalizer<BlobStoringPipelineResource>>()[code];

            text.ResourceNotFound.ShouldBeFalse();
            text.Value.ShouldNotBe(code);
        }
    }
}
