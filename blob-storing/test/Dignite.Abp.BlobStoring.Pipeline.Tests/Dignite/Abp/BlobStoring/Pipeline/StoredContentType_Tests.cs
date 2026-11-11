using System;
using Shouldly;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// <c>GetStoredContentType</c> / <c>SetStoredContentType</c>: a declaration on the container's configuration, read before
/// the save. Plain configuration objects; nothing is saved.
/// </summary>
public class StoredContentType_Tests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    public void Without_A_Declaration_The_Type_Should_Be_Stored_As_It_Is(string contentType)
    {
        new BlobContainerConfiguration().GetStoredContentType(contentType).ShouldBe(contentType);
    }

    [Fact]
    public void A_Declared_Type_Should_Be_Stored_As_Its_Target()
    {
        var container = new BlobContainerConfiguration().SetStoredContentType("image/jpeg", "image/webp");

        container.GetStoredContentType("image/jpeg").ShouldBe("image/webp");
        container.GetStoredContentType("IMAGE/JPEG").ShouldBe("image/webp");
        container.GetStoredContentType("image/png").ShouldBe("image/png");
    }

    [Fact]
    public void Declaring_A_Type_Again_Should_Replace_It_And_Keep_The_Others()
    {
        var container = new BlobContainerConfiguration()
            .SetStoredContentType("image/jpeg", "image/webp")
            .SetStoredContentType("image/bmp", "image/png")
            .SetStoredContentType("image/jpeg", "image/avif");

        container.GetStoredContentType("image/jpeg").ShouldBe("image/avif");
        container.GetStoredContentType("image/bmp").ShouldBe("image/png");
    }

    [Fact]
    public void A_Declaration_Should_Be_One_Step()
    {
        var container = new BlobContainerConfiguration()
            .SetStoredContentType("image/bmp", "image/jpeg")
            .SetStoredContentType("image/jpeg", "image/webp");

        container.GetStoredContentType("image/bmp").ShouldBe("image/jpeg");
    }

    [Fact]
    public void The_Default_Containers_Declarations_Should_Apply_Until_A_Container_Declares_Its_Own_Without_Changing_Them()
    {
        var defaults = new BlobContainerConfiguration().SetStoredContentType("image/jpeg", "image/webp");
        var container = new BlobContainerConfiguration(defaults);

        container.GetStoredContentType("image/jpeg").ShouldBe("image/webp");

        container.SetStoredContentType("image/bmp", "image/png");

        container.GetStoredContentType("image/jpeg").ShouldBe("image/webp");
        container.GetStoredContentType("image/bmp").ShouldBe("image/png");
        defaults.GetStoredContentType("image/bmp").ShouldBe("image/bmp");
    }

    [Theory]
    [InlineData("", "image/webp")]
    [InlineData("image/jpeg", " ")]
    public void An_Empty_Type_Should_Be_Refused(string contentType, string storedContentType)
    {
        Should.Throw<ArgumentException>(() => new BlobContainerConfiguration().SetStoredContentType(contentType, storedContentType));
    }
}
