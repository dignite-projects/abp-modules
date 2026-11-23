using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Imports;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The unfiled files kept by id of a container filed into directories by a path an extra property holds, against the database
/// provider of the concrete test class.
/// </summary>
public abstract class DirectoryPathImporter_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Property = "Test:DirectoryPath";

    private readonly DirectoryPathImporter _importer;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly DirectoryManager _directoryManager;
    private readonly IFileDescriptorRepository _fileRepository;
    private readonly IDirectoryDescriptorRepository _directoryRepository;

    protected DirectoryPathImporter_Tests()
    {
        _importer = GetRequiredService<DirectoryPathImporter>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _directoryManager = GetRequiredService<DirectoryManager>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
    }

    [Fact]
    public async Task A_Dry_Run_Should_Report_And_Change_Nothing()
    {
        var content = await NewContentAsync();

        var report = await ImportAsync(BlobManagementTestContainers.Documents, dryRun: true);

        report.DryRun.ShouldBeTrue();
        report.FilesRead.ShouldBe(5);
        report.FilesWithoutPath.ShouldBe(1);
        report.FilesFiled.Select(f => f.Path).OrderBy(p => p).ShouldBe(new[] { "Docs", "Docs/2026", "Docs/2026" });
        report.DirectoriesCreated.ShouldBe(new[] { "Docs", "Docs/2026" }, ignoreOrder: true);
        report.FilesSkipped.Single().FileId.ShouldBe(content.TooDeepId);

        (await WithUnitOfWorkAsync(() => _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Documents, null)))
            .ShouldBeEmpty();
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.MinutesId))).DirectoryId.ShouldBeNull();
    }

    [Fact]
    public async Task An_Import_Should_Build_The_Tree_File_The_Files_And_Be_Repeatable()
    {
        var content = await NewContentAsync();
        // A directory that exists already under the name is used as it is.
        var existing = await WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(BlobManagementTestContainers.Documents, "Docs", null));

        var report = await ImportAsync(BlobManagementTestContainers.Documents, dryRun: false);
        report.DirectoriesReused.ShouldBe(new[] { "Docs" });
        report.DirectoriesCreated.ShouldBe(new[] { "Docs/2026" });
        report.FilesFiled.Count.ShouldBe(3);

        var year = (await WithUnitOfWorkAsync(() =>
            _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Documents, existing.Id))).Single();
        year.Name.ShouldBe("2026");

        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.MinutesId))).DirectoryId.ShouldBe(year.Id);
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.NotesId))).DirectoryId.ShouldBe(year.Id);
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.IndexId))).DirectoryId.ShouldBe(existing.Id);
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.TooDeepId))).DirectoryId.ShouldBeNull();
        // A file an application saved under a name is not one to file.
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(content.SavedId))).DirectoryId.ShouldBeNull();

        // Run again: the filed files are no longer read, and nothing is created.
        var again = await ImportAsync(BlobManagementTestContainers.Documents, dryRun: false);
        again.FilesRead.ShouldBe(2);
        again.FilesFiled.ShouldBeEmpty();
        again.DirectoriesCreated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_A_Container_With_Directories_Should_Be_Imported()
    {
        var notAllowed = await Should.ThrowAsync<BusinessException>(() => ImportAsync(BlobManagementTestContainers.Files, dryRun: true));
        notAllowed.Code.ShouldBe(BlobManagementErrorCodes.DirectoriesNotAllowed);
    }

    private class Content
    {
        public Guid MinutesId { get; set; }
        public Guid NotesId { get; set; }
        public Guid IndexId { get; set; }
        public Guid TooDeepId { get; set; }
        public Guid SavedId { get; set; }
    }

    /// <summary>
    /// Files kept by id at the top of the container: two under <c>Docs/2026</c> (one written with spaces and a doubled
    /// separator), one under <c>Docs</c>, one without a path, one too deep to file - and a file an application saved,
    /// with a path that is not the importer's to follow. Every test has a database of its own.
    /// </summary>
    private async Task<Content> NewContentAsync()
    {
        var content = new Content();
        content.MinutesId = (await CreateFileAsync("Docs/2026")).Id;
        content.NotesId = (await CreateFileAsync(" Docs //2026/ ")).Id;
        content.IndexId = (await CreateFileAsync("Docs")).Id;
        await CreateFileAsync(null);
        content.TooDeepId = (await CreateFileAsync(
            string.Join("/", Enumerable.Range(1, DirectoryDescriptorConsts.MaxDepth + 1).Select(i => "level" + i)))).Id;
        content.SavedId = (await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.SaveBlobAsync(BlobManagementTestContainers.Documents, Guid.NewGuid().ToString("N") + ".pdf", stream);
        })).Id;
        return content;
    }

    /// <summary>
    /// A file kept by id at the top of the container, with <paramref name="path"/> in the extra property the importer
    /// reads - written with the file, as an application that migrates its own library in writes it.
    /// </summary>
    private Task<FileDescriptor> CreateFileAsync(string? path)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));
            var args = new CreateFileArgs();
            if (path != null)
            {
                args.ExtraProperties[Property] = path;
            }

            return await _fileDescriptorManager.CreateFileAsync(
                BlobManagementTestContainers.Documents, Guid.NewGuid().ToString("N") + ".pdf", stream, args);
        });
    }

    private Task<DirectoryPathImportReport> ImportAsync(string containerName, bool dryRun)
    {
        return WithUnitOfWorkAsync(() => _importer.ImportAsync(new DirectoryPathImportRequest(containerName, Property, dryRun)));
    }
}
