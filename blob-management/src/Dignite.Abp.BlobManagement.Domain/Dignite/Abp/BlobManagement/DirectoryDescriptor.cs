using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A directory of a managed container that allows directories: the hierarchy of the files people upload there, and
/// nothing more. The directories of one container form one tree per tenant. Every rule that keeps it a tree - unique
/// sibling names, a parent in the same container, no cycles, a bounded depth, deletion only when empty - lives in
/// <see cref="DirectoryManager"/>.
/// <para>
/// Soft-deleted, so a deleted directory's files keep naming it (a trash, later, restores both). Its full name,
/// <c>Dignite.Abp.BlobManagement.DirectoryDescriptor</c>, is the resource name of the grants on it: never rename the class.
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
        NameKey = CreateNameKey(tenantId, containerName, parentId, name);
    }

    /// <summary>The container the directory belongs to.</summary>
    public virtual string ContainerName { get; protected set; } = default!;

    /// <summary>Unique among its live siblings, without regard to case (<see cref="NameKey"/>).</summary>
    public virtual string Name { get; protected set; } = default!;

    /// <summary>The parent directory, or <c>null</c> at the top of the tree.</summary>
    public virtual Guid? ParentId { get; protected set; }

    /// <summary>
    /// The ids from the top of the tree down to and including this directory, each as <c>Guid.ToString("N")</c>, joined
    /// by <see cref="DirectoryDescriptorConsts.PathSeparator"/>. A check that a permission on a directory reaches below it
    /// takes the ancestors from here instead of reading them one level at a time, and a subtree is the directories whose
    /// path starts with this one's plus the separator.
    /// <para>
    /// Ids, not per-level counters like ABP's <c>OrganizationUnit.Code</c>: a counter is assigned check-then-act, so two
    /// simultaneous creates can get the same one, and it is reused after a soft delete, which a restore would collide
    /// with. An id path is unique by construction.
    /// </para>
    /// </summary>
    public virtual string Path { get; protected set; } = default!;

    /// <summary>Position among its siblings.</summary>
    public virtual int Order { get; protected set; }

    /// <summary>
    /// Whether the directory has permissions of its own: it, and everything below it, is then seen - and its
    /// subdirectories managed - only by those granted on it (and by holders of the module-wide permissions). A directory
    /// that is not restricted follows the nearest restricted directory above it, or its container. The application
    /// services decide; this is only the mark, kept as a column so that listing a level need not ask the grant store
    /// about every directory in it.
    /// </summary>
    public virtual bool IsRestricted { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// On a live directory, the SHA-256 (upper-case hex) of its tenant, container, parent and name, the name
    /// upper-cased (<see cref="CreateNameKey"/>); <c>null</c> on a deleted one. Unique in the database, so two
    /// simultaneous creates, renames or moves cannot leave two live siblings of one name - the sibling-name check of
    /// <see cref="DirectoryManager"/> is check-then-act, and this is the arbiter. Cleared on delete, so a deleted
    /// directory does not hold its name, as <c>FileDescriptor.NameKey</c> is.
    /// </summary>
    public virtual string? NameKey { get; protected set; }

    /// <summary>
    /// Renames the directory. Uniqueness among siblings is checked by <see cref="DirectoryManager.RenameAsync"/>; never
    /// call this to bypass it.
    /// </summary>
    public virtual void Rename(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
        RefreshNameKey();
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
        RefreshNameKey();
    }

    public virtual void SetOrder(int order)
    {
        Order = order;
    }

    public virtual void SetRestricted(bool isRestricted)
    {
        IsRestricted = isRestricted;
    }

    /// <summary>
    /// Gives up the <see cref="NameKey"/>; done before the directory is deleted, so the deleted row does not keep a new
    /// sibling from taking its name.
    /// </summary>
    public virtual void ReleaseNameKey()
    {
        NameKey = null;
    }

    /// <summary>
    /// Moves this directory's path along with an ancestor's: <paramref name="oldPrefix"/>, the ancestor's path before it
    /// moved, is replaced by <paramref name="newPrefix"/>, its path now. Called by <see cref="DirectoryManager.MoveAsync"/>
    /// for every directory below the one it moved; never call it otherwise. The name key names the parent, not the path,
    /// so it does not change.
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

    /// <summary>The ids of the directories above this one, nearest first (the parent, then its parent, up to the top).</summary>
    public virtual List<Guid> GetAncestorIds()
    {
        var segments = Path.Split(DirectoryDescriptorConsts.PathSeparator);
        var ids = new List<Guid>(segments.Length - 1);
        for (var i = segments.Length - 2; i >= 0; i--)
        {
            ids.Add(Guid.ParseExact(segments[i], "N"));
        }

        return ids;
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
    /// SHA-256, as upper-case hex, of <c>{tenant}:{container}:{parent}:{NAME}</c> - the tenant as 32 hexadecimal
    /// digits or <c>host</c>, the parent as 32 hexadecimal digits or <c>-</c>, the name upper-cased with
    /// the invariant culture, so names that differ only in case are one name, as they are on the desktop file systems
    /// people move folders to and from. A hash, since unique-index keys are size-limited and a name may be long.
    /// </summary>
    public static string CreateNameKey(Guid? tenantId, string containerName, Guid? parentId, string name)
    {
        var key = (tenantId?.ToString("N") ?? "host") + ":" + containerName + ":" +
                  (parentId?.ToString("N") ?? "-") + ":" + name.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>The <see cref="NameKey"/> after a rename or a move; a deleted directory, which has none, takes none.</summary>
    protected virtual void RefreshNameKey()
    {
        if (NameKey != null)
        {
            NameKey = CreateNameKey(TenantId, ContainerName, ParentId, Name);
        }
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
