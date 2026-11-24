using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A directory of a managed container that allows directories: a further classification of the files a business
/// system keeps there, and nothing more. The directories of one container form one tree per tenant. Every rule that keeps it a tree - unique
/// sibling names, a parent in the same container, no cycles, a bounded depth, deletion only when empty - lives in
/// <see cref="DirectoryManager"/>.
/// <para>
/// Soft-deleted, as ABP's own trees are (<c>OrganizationUnit</c>): a deleted directory stays as a row nobody lists, and
/// gives its name up to whatever is created in its place. It has no permissions of its own: what a user may do with
/// the directories of a container is the container's permission.
/// </para>
/// </summary>
public class DirectoryDescriptor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    protected DirectoryDescriptor()
    {
    }

    /// <param name="parentPath">The <see cref="Path"/> of <paramref name="parentId"/>; <c>null</c> at the top of the tree.</param>
    public DirectoryDescriptor(
        Guid id,
        string containerName,
        string name,
        Guid? parentId,
        string? parentPath,
        int order,
        Guid? tenantId = null)
        : base(id)
    {
        ContainerName = Check.NotNullOrWhiteSpace(containerName, nameof(containerName), FileDescriptorConsts.MaxContainerNameLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
        ParentId = parentId;
        Path = BuildPath(id, parentId, parentPath);
        Order = order;
        TenantId = tenantId;
    }

    /// <summary>The container the directory belongs to.</summary>
    public virtual string ContainerName { get; protected set; } = default!;

    /// <summary>
    /// Unique among its siblings, compared as it is written: <see cref="DirectoryManager"/> checks it, as ABP checks
    /// the display name of an organization unit among its siblings. A display name, so a change of case is a new name.
    /// </summary>
    public virtual string Name { get; protected set; } = default!;

    /// <summary>The parent directory, or <c>null</c> at the top of the tree.</summary>
    public virtual Guid? ParentId { get; protected set; }

    /// <summary>
    /// The ids from the top of the tree down to and including this directory, each as <c>Guid.ToString("N")</c>, joined
    /// by <see cref="DirectoryDescriptorConsts.PathSeparator"/>. A subtree is the directories whose path starts with this
    /// one's plus the separator: a move rewrites it in one pass, and a check against moving a directory into itself and
    /// the depth limit read it without walking the tree.
    /// <para>
    /// Ids, not per-level counters like ABP's <c>OrganizationUnit.Code</c>: a counter is assigned check-then-act, so two
    /// simultaneous creates can get the same one. An id path is unique by construction.
    /// </para>
    /// </summary>
    public virtual string Path { get; protected set; } = default!;

    /// <summary>Position among its siblings.</summary>
    public virtual int Order { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// Renames the directory. Uniqueness among siblings is checked by <see cref="DirectoryManager.RenameAsync"/>; never
    /// call this to bypass it.
    /// </summary>
    public virtual void Rename(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
    }

    /// <summary>
    /// Re-parents the directory and sets its position. The tree, cycle and depth checks, and rewriting the paths of the
    /// directories below it, live in <see cref="DirectoryManager.MoveAsync"/>; never call this to bypass them.
    /// </summary>
    /// <param name="parentPath">The <see cref="Path"/> of <paramref name="parentId"/>; <c>null</c> at the top of the tree.</param>
    public virtual void MoveTo(Guid? parentId, string? parentPath, int order)
    {
        ParentId = parentId;
        Path = BuildPath(Id, parentId, parentPath);
        Order = order;
    }

    public virtual void SetOrder(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Moves this directory's path along with an ancestor's: <paramref name="oldPrefix"/>, the ancestor's path before it
    /// moved, is replaced by <paramref name="newPrefix"/>, its path now. Called by <see cref="DirectoryManager.MoveAsync"/>
    /// for every directory below the one it moved; never call it otherwise.
    /// </summary>
    public virtual void ReplacePathPrefix(string oldPrefix, string newPrefix)
    {
        Check.NotNullOrWhiteSpace(oldPrefix, nameof(oldPrefix));
        Check.NotNullOrWhiteSpace(newPrefix, nameof(newPrefix));

        if (!Path.StartsWith(oldPrefix + DirectoryDescriptorConsts.PathSeparator, StringComparison.Ordinal))
        {
            throw new AbpException($"The directory '{Id}' is not below the path '{oldPrefix}'.");
        }

        Path = Check.Length(newPrefix + Path.Substring(oldPrefix.Length), nameof(Path), DirectoryDescriptorConsts.MaxPathLength)!;
    }

    /// <summary>How many levels down the directory is: 1 at the top of its tree.</summary>
    public virtual int GetDepth()
    {
        return GetDepth(Path);
    }

    /// <summary>Whether this directory is <paramref name="directory"/> or somewhere below it.</summary>
    public virtual bool IsAtOrBelow(DirectoryDescriptor directory)
    {
        Check.NotNull(directory, nameof(directory));
        return Path == directory.Path ||
               Path.StartsWith(directory.Path + DirectoryDescriptorConsts.PathSeparator, StringComparison.Ordinal);
    }

    /// <summary>How many levels a <see cref="Path"/> has.</summary>
    public static int GetDepth(string path)
    {
        Check.NotNullOrWhiteSpace(path, nameof(path));

        var depth = 1;
        foreach (var character in path)
        {
            if (character == DirectoryDescriptorConsts.PathSeparator)
            {
                depth++;
            }
        }

        return depth;
    }

    /// <summary>
    /// The path of directory <paramref name="id"/> under <paramref name="parentId"/>, whose path is
    /// <paramref name="parentPath"/>. The two parent arguments must agree, so a path never names a parent the directory
    /// is not under.
    /// </summary>
    protected static string BuildPath(Guid id, Guid? parentId, string? parentPath)
    {
        var segment = id.ToString("N");
        if (!parentId.HasValue)
        {
            if (parentPath != null)
            {
                throw new ArgumentException("A directory at the top of its tree has no parent path.", nameof(parentPath));
            }

            return segment;
        }

        if (parentPath == null ||
            !parentPath.EndsWith(parentId.Value.ToString("N"), StringComparison.Ordinal))
        {
            throw new ArgumentException($"The parent path must be the path of the directory '{parentId}'.", nameof(parentPath));
        }

        return Check.Length(parentPath + DirectoryDescriptorConsts.PathSeparator + segment, "path", DirectoryDescriptorConsts.MaxPathLength)!;
    }
}
