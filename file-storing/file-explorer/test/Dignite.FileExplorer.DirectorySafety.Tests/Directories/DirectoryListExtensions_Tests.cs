using System;
using Dignite.FileExplorer.Directories;
using Shouldly;
using Xunit;

namespace Dignite.FileExplorer.DirectorySafety.Tests.Directories;

public class DirectoryListExtensions_Tests
{
    [Fact]
    public void BuildTree_ShouldStopWhenSourceContainsACycle()
    {
        var first = new DirectoryDescriptorInfoDto
        {
            Id = Guid.NewGuid()
        };
        var second = new DirectoryDescriptorInfoDto
        {
            Id = Guid.NewGuid(),
            ParentId = first.Id
        };
        first.ParentId = second.Id;

        var tree = new[] { first, second }.BuildTree();

        tree.Count.ShouldBe(1);
        tree[0].Children.Count.ShouldBe(1);
        tree.ToLevelList().Count.ShouldBe(2);
    }
}
