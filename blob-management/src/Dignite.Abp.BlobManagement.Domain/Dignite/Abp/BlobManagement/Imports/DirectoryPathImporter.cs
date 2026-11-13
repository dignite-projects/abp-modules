using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;

namespace Dignite.Abp.BlobManagement.Imports;

/// <summary>
/// Files the unfiled uploaded files of a container into directories, by a path an extra property of each file holds.
/// Whoever stored the files wrote the path - an application that migrates a file library of its own in - and the
/// operator names the property: Blob Management knows nothing of the writer.
/// <para>
/// Repeatable: a directory is looked up by its name key before it is created (so an existing one is used, whatever its
/// case, and a second run creates nothing the first created), and only files at the top of the container are read, so a
/// file filed before - by an import or by a person - is left where it is. A dry run reports what an import would do
/// and writes nothing; it is the default.
/// </para>
/// <para>
/// It runs in the current tenant and the caller's unit of work, and authorizes nobody: it is an operator's tool, called
/// from code. The container must allow directories.
/// </para>
/// </summary>
public class DirectoryPathImporter : DomainService
{
    /// <summary>How many files are read at a time.</summary>
    protected const int PageSize = 500;

    protected IFileDescriptorRepository FileRepository { get; }

    protected IDirectoryDescriptorRepository DirectoryRepository { get; }

    protected DirectoryManager DirectoryManager { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    public DirectoryPathImporter(
        IFileDescriptorRepository fileRepository,
        IDirectoryDescriptorRepository directoryRepository,
        DirectoryManager directoryManager,
        FileDescriptorManager fileDescriptorManager)
    {
        FileRepository = fileRepository;
        DirectoryRepository = directoryRepository;
        DirectoryManager = directoryManager;
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual async Task<DirectoryPathImportReport> ImportAsync(
        DirectoryPathImportRequest request,
        CancellationToken cancellationToken = default)
    {
        DirectoryManager.CheckDirectoriesAllowed(request.ContainerName);

        var report = new DirectoryPathImportReport(request.ContainerName, request.PropertyName, request.DryRun);
        var directories = new Dictionary<string, Guid>(StringComparer.Ordinal);

        // A filed file leaves the set read here, so in an import the next page starts after the files that stayed; in a
        // dry run nothing leaves it.
        var stayed = 0;
        while (true)
        {
            var files = await FileRepository.GetUnfiledListAsync(
                request.ContainerName, request.DryRun ? report.FilesRead : stayed, PageSize, cancellationToken);
            if (files.Count == 0)
            {
                break;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                report.FilesRead++;
                if (!await ImportFileAsync(request, report, directories, file, cancellationToken))
                {
                    stayed++;
                }
            }

            if (files.Count < PageSize)
            {
                break;
            }
        }

        return report;
    }

    /// <summary>Files one file; returns whether it was filed (would be, in a dry run).</summary>
    protected virtual async Task<bool> ImportFileAsync(
        DirectoryPathImportRequest request,
        DirectoryPathImportReport report,
        Dictionary<string, Guid> directories,
        FileDescriptor file,
        CancellationToken cancellationToken)
    {
        var raw = file.GetProperty(request.PropertyName)?.ToString();
        var segments = Split(raw, request.Separator);
        if (segments.Count == 0)
        {
            report.FilesWithoutPath++;
            return false;
        }

        var skip = Validate(segments);
        if (skip != null)
        {
            report.FilesSkipped.Add(new DirectoryPathImportReport.SkippedFile(file.Id, raw!, skip));
            return false;
        }

        Guid? parentId = null;
        for (var i = 0; i < segments.Count; i++)
        {
            parentId = await GetOrCreateDirectoryAsync(
                request, report, directories, parentId, segments[i], Describe(segments, i), cancellationToken);
        }

        if (!request.DryRun)
        {
            await FileDescriptorManager.MoveAsync(file, parentId, cancellationToken);
        }

        report.FilesFiled.Add(new DirectoryPathImportReport.FiledFile(file.Id, Describe(segments, segments.Count - 1)));
        return true;
    }

    /// <summary>
    /// The directory named <paramref name="name"/> under <paramref name="parentId"/>: the one that holds the name
    /// already (its name key, so case aside), or a new one. In a dry run nothing is created; the directory is given an
    /// id of its own for the rest of the run.
    /// </summary>
    protected virtual async Task<Guid> GetOrCreateDirectoryAsync(
        DirectoryPathImportRequest request,
        DirectoryPathImportReport report,
        Dictionary<string, Guid> directories,
        Guid? parentId,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        var nameKey = DirectoryDescriptor.CreateNameKey(CurrentTenant.Id, request.ContainerName, parentId, name);
        if (directories.TryGetValue(nameKey, out var known))
        {
            return known;
        }

        // Under a directory a dry run would create, the key names an id that is in no database: nothing is found.
        Guid id;
        var existing = await DirectoryRepository.FindByNameKeyAsync(nameKey, cancellationToken);
        if (existing != null)
        {
            id = existing.Id;
            report.DirectoriesReused.Add(description);
        }
        else if (request.DryRun)
        {
            id = GuidGenerator.Create();
            report.DirectoriesCreated.Add(description);
        }
        else
        {
            id = (await DirectoryManager.CreateAsync(request.ContainerName, name, parentId, cancellationToken)).Id;
            report.DirectoriesCreated.Add(description);
        }

        directories[nameKey] = id;
        return id;
    }

    /// <summary>The path's directory names: split, trimmed, empty ones dropped (so <c>/a//b/</c> is <c>a/b</c>).</summary>
    protected virtual List<string> Split(string? path, char separator)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return new List<string>();
        }

        return path!.Split(separator)
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0)
            .ToList();
    }

    /// <summary>Why the path cannot be a chain of directories, or <c>null</c>.</summary>
    protected virtual string? Validate(List<string> segments)
    {
        if (segments.Count > DirectoryDescriptorConsts.MaxDepth)
        {
            return $"it is deeper than {DirectoryDescriptorConsts.MaxDepth} levels";
        }

        var tooLong = segments.FirstOrDefault(segment => segment.Length > DirectoryDescriptorConsts.MaxNameLength);
        return tooLong == null
            ? null
            : $"the name '{tooLong}' is longer than {DirectoryDescriptorConsts.MaxNameLength} characters";
    }

    /// <summary>The path down to segment <paramref name="last"/>.</summary>
    protected virtual string Describe(List<string> segments, int last)
    {
        return string.Join("/", segments.Take(last + 1));
    }
}
