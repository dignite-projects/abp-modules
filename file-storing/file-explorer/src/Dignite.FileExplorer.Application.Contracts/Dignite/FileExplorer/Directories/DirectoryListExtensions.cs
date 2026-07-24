using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.FileExplorer.Directories;

public static class DirectoryListExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="source"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static DirectoryDescriptorInfoDto FindById([NotNull] this IEnumerable<DirectoryDescriptorInfoDto> source, Guid id)
    {
        return FindById(source, id, new HashSet<Guid>());
    }

    private static DirectoryDescriptorInfoDto FindById(IEnumerable<DirectoryDescriptorInfoDto> source, Guid id, HashSet<Guid> visited)
    {
        foreach (var item in source)
        {
            if (!visited.Add(item.Id))
            {
                continue;
            }

            if (item.Id == id)
            {
                return item;
            }

            if (item.Children != null && item.Children.Any())
            {
                var result = FindById(item.Children, id, visited);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static IReadOnlyList<DirectoryDescriptorInfoDto> ToLevelList([NotNull] this IReadOnlyList<DirectoryDescriptorInfoDto> source)
    {
        var result = new List<DirectoryDescriptorInfoDto>();
        var visited = new HashSet<Guid>();
        foreach (var ou in source)
        {
            if (!visited.Add(ou.Id))
            {
                continue;
            }

            result.Add(ou);
            FindChildren(result, ou, visited);
        }
        return result;
    }

    public static IReadOnlyList<DirectoryDescriptorInfoDto> BuildTree([NotNull] this IReadOnlyList<DirectoryDescriptorInfoDto> source)
    {
        if (source.Any())
        {
            var parentId = source.First().ParentId;
            var tree = new List<DirectoryDescriptorInfoDto>();
            tree.AddRange(source.Where(p => p.ParentId == parentId).ToList());
            foreach (var ou in tree)
            {
                AddChildren(ou, source, new HashSet<Guid> { ou.Id });
            }
            return tree;
        }
        return source;
    }

    public static IReadOnlyList<DirectoryDescriptorInfoDto> GetParentList([NotNull] this DirectoryDescriptorInfoDto directory, IEnumerable<DirectoryDescriptorInfoDto> source)
    {
        var result = new List<DirectoryDescriptorInfoDto>();
        FindParent(directory, source, result, new HashSet<Guid> { directory.Id });
        result.Reverse();
        return result;
    }

    private static void FindChildren(List<DirectoryDescriptorInfoDto> list, DirectoryDescriptorInfoDto directory, HashSet<Guid> visited)
    {
        if (directory.Children != null && directory.Children.Any())
        {
            foreach (var c in directory.Children)
            {
                if (!visited.Add(c.Id))
                {
                    continue;
                }

                list.Add(c);
                FindChildren(list, c, visited);
            }
        }
    }

    private static void AddChildren(DirectoryDescriptorInfoDto parent, IReadOnlyList<DirectoryDescriptorInfoDto> list, HashSet<Guid> visited)
    {
        var children = list.Where(p => p.ParentId == parent.Id).ToList();
        if (children.Any())
        {
            foreach (var ou in children)
            {
                if (!visited.Add(ou.Id))
                {
                    continue;
                }

                parent.AddChild(ou);
                AddChildren(ou, list, visited);
            }
        }
    }

    private static void FindParent(DirectoryDescriptorInfoDto directory, IEnumerable<DirectoryDescriptorInfoDto> source, List<DirectoryDescriptorInfoDto> result, HashSet<Guid> visited)
    {
        if (directory.ParentId.HasValue)
        {
            var parent = source.FindById(directory.ParentId.Value);
            if (parent != null && visited.Add(parent.Id))
            {
                result.Add(parent);
                FindParent(parent, source, result, visited);
            }
        }
    }
}
