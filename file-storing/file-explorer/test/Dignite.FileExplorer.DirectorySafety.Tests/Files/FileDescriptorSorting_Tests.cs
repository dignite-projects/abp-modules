using Dignite.FileExplorer.Files;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.FileExplorer.DirectorySafety.Tests.Files;

public class FileDescriptorSorting_Tests
{
    [Fact]
    public void Normalize_ShouldAllowKnownFieldAndDirection()
    {
        FileDescriptorSorting.Normalize("Name DESC", "CreationTime desc")
            .ShouldBe("Name desc");
    }

    [Theory]
    [InlineData("Name desc, Size asc")]
    [InlineData("Name.ToString()")]
    [InlineData("Unknown asc")]
    [InlineData("Name descending")]
    public void Normalize_ShouldRejectUnsupportedSorting(string sorting)
    {
        Should.Throw<BusinessException>(() =>
            FileDescriptorSorting.Normalize(sorting, "CreationTime desc"));
    }
}
