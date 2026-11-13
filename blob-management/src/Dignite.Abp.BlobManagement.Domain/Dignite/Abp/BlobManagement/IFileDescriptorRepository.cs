using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The descriptors of the current tenant. Every query sees live descriptors only (ABP's soft-delete filter).
/// </summary>
public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    /// <summary>
    /// The live descriptor an application saved under <paramref name="name"/> in <paramref name="containerName"/>, if
    /// any: a file addressed by its name. An uploaded file of the same name is not it.
    /// </summary>
    Task<FileDescriptor?> FindByNameAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>A live descriptor whose upload hashed to <paramref name="hash"/>, if any - the source of a dedup.</summary>
    Task<FileDescriptor?> FindByHashAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The bytes the current tenant stores: the <see cref="FileDescriptor.Size"/> of each physical BLOB named by a live
    /// descriptor, counted once however many descriptors share it.
    /// </summary>
    Task<long> GetUsedStorageSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The live descriptor of the container whose physical BLOB is <paramref name="blobName"/>, if any - an uploaded
    /// file's public address. Descriptors that share the BLOB share its bytes, so any of them answers.
    /// </summary>
    Task<FileDescriptor?> FindByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a live descriptor names <paramref name="blobName"/> as its physical BLOB.</summary>
    Task<bool> IsBlobInUseAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The files of one place of a container: <paramref name="directoryId"/>, or with <c>null</c> its top - where the
    /// files applications saved under a name are as well. <paramref name="filter"/> is matched against the name,
    /// whatever its case; <paramref name="sorting"/> is checked by <see cref="FileDescriptorSorting.Normalize"/>, which
    /// ends every order with the id, so two pages never share or miss a file.
    /// </summary>
    Task<List<FileDescriptor>> GetListAsync(
        string containerName,
        Guid? directoryId,
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    /// <summary>How many files <see cref="GetListAsync"/> finds without paging.</summary>
    Task<long> GetCountAsync(
        string containerName,
        Guid? directoryId,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a live file is filed in <paramref name="directoryId"/>.</summary>
    Task<bool> AnyInDirectoryAsync(Guid directoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The uploaded files of <paramref name="containerName"/> filed in no directory - not the ones addressed by a name
    /// - by id, a page at a time.
    /// </summary>
    Task<List<FileDescriptor>> GetUnfiledListAsync(
        string containerName,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
