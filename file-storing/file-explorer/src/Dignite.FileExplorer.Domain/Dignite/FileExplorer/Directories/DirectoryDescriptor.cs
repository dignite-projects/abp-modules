using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.FileExplorer.Directories;

public class DirectoryDescriptor : AuditedAggregateRoot<Guid>, IMultiTenant
{
    protected DirectoryDescriptor()
    {
    }

    public DirectoryDescriptor(Guid id, string containerName, string name, Guid? parentId, int order, Guid? tenantId)
        : base(id)
    {
        ContainerName = containerName;
        Name = name;
        ParentId = parentId;
        Order = order;
        TenantId = tenantId;
    }

    /// <summary>
    /// Container name of blob
    /// </summary>
    public string ContainerName { get; protected set; }

    /// <summary>
    ///
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    ///
    /// </summary>
    public Guid? ParentId { get; protected set; }

    /// <summary>
    ///
    /// </summary>
    public int Order { get; protected set; }

    public Guid? TenantId { get; protected set; }

    /// <summary>
    /// Renames the directory. Uniqueness among siblings is enforced by <c>DirectoryManager</c>.
    /// </summary>
    public void Rename(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
    }

    /// <summary>
    /// Re-parents the directory and sets its position. Cycle and container/owner checks live in
    /// <c>DirectoryManager.MoveAsync</c>; never mutate the parent directly to bypass them.
    /// </summary>
    public void MoveTo(Guid? parentId, int order)
    {
        ParentId = parentId;
        Order = order;
    }

    /// <summary>
    /// Sets the sibling ordering position.
    /// </summary>
    public void SetOrder(int order)
    {
        Order = order;
    }
}
