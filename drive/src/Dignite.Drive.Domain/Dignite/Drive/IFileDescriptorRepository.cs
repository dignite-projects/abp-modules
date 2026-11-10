using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Drive;

/// <summary>
/// The descriptors of the current tenant. Every query sees live descriptors only (ABP's soft-delete filter).
/// </summary>
public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    /// <summary>The live descriptor named <paramref name="name"/> in <paramref name="containerName"/>, if any.</summary>
    Task<FileDescriptor?> FindByNameAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>A live descriptor whose upload hashed to <paramref name="hash"/>, if any - the source of a dedup.</summary>
    Task<FileDescriptor?> FindByHashAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a live descriptor names <paramref name="blobName"/> as its physical BLOB.</summary>
    Task<bool> IsBlobInUseAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);
}
