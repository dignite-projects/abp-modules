using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The directories of the current tenant, live ones only (ABP's soft-delete filter).
/// </summary>
public interface IDirectoryDescriptorRepository : IBasicRepository<DirectoryDescriptor, Guid>
{
    /// <summary>Whether a live directory holds <paramref name="nameKey"/> (<see cref="DirectoryDescriptor.CreateNameKey"/>).</summary>
    Task<bool> NameKeyExistsAsync(string nameKey, CancellationToken cancellationToken = default);

    /// <summary>The live directory that holds <paramref name="nameKey"/>, or <c>null</c>.</summary>
    Task<DirectoryDescriptor?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The direct children of <paramref name="parentId"/> (<c>null</c>: the top of the container), by position.
    /// </summary>
    Task<List<DirectoryDescriptor>> GetChildrenAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>The highest <see cref="DirectoryDescriptor.Order"/> under <paramref name="parentId"/>, or 0.</summary>
    Task<int> GetMaxOrderAsync(
        string containerName,
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>The directories of <paramref name="ids"/> that exist, in no particular order.</summary>
    Task<List<DirectoryDescriptor>> GetListByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every directory below the one whose <see cref="DirectoryDescriptor.Path"/> is <paramref name="path"/>, at any
    /// depth, not the directory itself. Subject to the data filters like every query here: a caller that needs deleted
    /// directories too disables the soft-delete filter.
    /// </summary>
    Task<List<DirectoryDescriptor>> GetDescendantsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Those of <paramref name="ids"/> that have a live subdirectory.</summary>
    Task<List<Guid>> GetIdsWithChildrenAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="id"/> has a live subdirectory.</summary>
    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);
}
