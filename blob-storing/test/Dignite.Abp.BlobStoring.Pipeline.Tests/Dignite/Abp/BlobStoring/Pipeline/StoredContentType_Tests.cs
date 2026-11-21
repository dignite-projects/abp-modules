using System;
using System.IO;
using Shouldly;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// <c>GetStoredContentType</c> / <c>SetStoredContentType</c>: a declaration on the container's configuration, read before
/// the save - for some content when it has a condition. Plain configuration objects; nothing is saved.
/// </summary>
public class StoredContentType_Tests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    public void Without_A_Declaration_The_Type_Should_Be_Stored_As_It_Is(string contentType)
    {
        new BlobContainerConfiguration().GetStoredContentType(contentType, Content()).ShouldBe(contentType);
    }

    [Fact]
    public void A_Declared_Type_Should_Be_Stored_As_Its_Target()
    {
        var container = new BlobContainerConfiguration().SetStoredContentType("image/jpeg", "image/webp");

        container.GetStoredContentType("image/jpeg", Content()).ShouldBe("image/webp");
        container.GetStoredContentType("IMAGE/JPEG", Content()).ShouldBe("image/webp");
        container.GetStoredContentType("image/png", Content()).ShouldBe("image/png");
    }

    /// <summary>A declaration without a condition does not read the content, which then need not be seekable.</summary>
    [Fact]
    public void A_Declaration_Without_A_Condition_Should_Not_Read_The_Content()
    {
        var container = new BlobContainerConfiguration().SetStoredContentType("image/jpeg", "image/webp");

        container.GetStoredContentType("image/jpeg", new UnreadableStream()).ShouldBe("image/webp");
    }

    [Fact]
    public void A_Conditional_Declaration_Should_Apply_To_The_Content_It_Holds_For()
    {
        var container = new BlobContainerConfiguration()
            .SetStoredContentType("image/png", "image/webp", (_, content) => content.Length >= 10);

        container.GetStoredContentType("image/png", Content(10)).ShouldBe("image/webp");
        container.GetStoredContentType("image/png", Content(9)).ShouldBe("image/png");
    }

    /// <summary>
    /// The condition gets the container's configuration and the content from its start, and may move the position, which
    /// is restored for the caller.
    /// </summary>
    [Fact]
    public void A_Condition_Should_Read_From_The_Start_And_Leave_The_Position_As_It_Was()
    {
        BlobContainerConfiguration? seen = null;
        var container = new BlobContainerConfiguration();
        container.SetStoredContentType("image/png", "image/webp", (configuration, content) =>
        {
            seen = configuration;
            var first = content.ReadByte();
            content.Position = content.Length;
            return first == 7;
        });
        using var content = new MemoryStream([7, 8, 9]);
        content.Position = 2;

        container.GetStoredContentType("image/png", content).ShouldBe("image/webp");

        content.Position.ShouldBe(2);
        seen.ShouldBeSameAs(container);
    }

    [Fact]
    public void A_Conditional_Declaration_Should_Need_A_Seekable_Content()
    {
        var container = new BlobContainerConfiguration().SetStoredContentType("image/png", "image/webp", (_, _) => true);

        Should.Throw<ArgumentException>(() => container.GetStoredContentType("image/png", new UnreadableStream()));
    }

    [Fact]
    public void Declaring_A_Type_Again_Should_Replace_It_And_Keep_The_Others()
    {
        var container = new BlobContainerConfiguration()
            .SetStoredContentType("image/jpeg", "image/webp", (_, _) => false)
            .SetStoredContentType("image/bmp", "image/png")
            .SetStoredContentType("image/jpeg", "image/avif");

        container.GetStoredContentType("image/jpeg", Content()).ShouldBe("image/avif");
        container.GetStoredContentType("image/bmp", Content()).ShouldBe("image/png");
    }

    [Fact]
    public void A_Declaration_Should_Be_One_Step()
    {
        var container = new BlobContainerConfiguration()
            .SetStoredContentType("image/bmp", "image/jpeg")
            .SetStoredContentType("image/jpeg", "image/webp");

        container.GetStoredContentType("image/bmp", Content()).ShouldBe("image/jpeg");
    }

    [Fact]
    public void The_Default_Containers_Declarations_Should_Apply_Until_A_Container_Declares_Its_Own_Without_Changing_Them()
    {
        var defaults = new BlobContainerConfiguration().SetStoredContentType("image/jpeg", "image/webp");
        var container = new BlobContainerConfiguration(defaults);

        container.GetStoredContentType("image/jpeg", Content()).ShouldBe("image/webp");

        container.SetStoredContentType("image/bmp", "image/png");

        container.GetStoredContentType("image/jpeg", Content()).ShouldBe("image/webp");
        container.GetStoredContentType("image/bmp", Content()).ShouldBe("image/png");
        defaults.GetStoredContentType("image/bmp", Content()).ShouldBe("image/bmp");
    }

    [Theory]
    [InlineData("", "image/webp")]
    [InlineData("image/jpeg", " ")]
    public void An_Empty_Type_Should_Be_Refused(string contentType, string storedContentType)
    {
        Should.Throw<ArgumentException>(() => new BlobContainerConfiguration().SetStoredContentType(contentType, storedContentType));
    }

    private static MemoryStream Content(int length = 4) => new(new byte[length]);

    /// <summary>A stream that can be neither read nor sought: any attempt fails the test.</summary>
    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
