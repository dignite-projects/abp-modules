using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace Dignite.FileExplorer;

public class FileExplorerDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly FileExplorerTestData _fileExplorerTestData;

    public FileExplorerDataSeedContributor(
        IGuidGenerator guidGenerator, ICurrentTenant currentTenant,
        IFileDescriptorRepository fileDescriptorRepository,
        FileExplorerTestData fileExplorerTestData)
    {
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _fileDescriptorRepository = fileDescriptorRepository;
        _fileExplorerTestData = fileExplorerTestData;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context?.TenantId))
        {
            await SeedFileDescriptorsAsync();
        }
    }

    private async Task SeedFileDescriptorsAsync()
    {
        var file1 = new FileDescriptor(
            _guidGenerator.Create(),
            _fileExplorerTestData.ContainerName1,
            _fileExplorerTestData.BlobName1,
            "testBlobName1.txt",
            "text/plain",
            "",
            null,
            _fileExplorerTestData.EntityId,
            _currentTenant.Id);
        file1.SetMd5("00000000000000000000000000000001");
        file1.SetReferBlobName("");
        await _fileDescriptorRepository.InsertAsync(file1, autoSave: true);

        var file2 = new FileDescriptor(
            _guidGenerator.Create(),
            _fileExplorerTestData.ContainerName2,
            _fileExplorerTestData.BlobName2,
            "testBlobName2.txt",
            "text/plain",
            "",
            null,
            _fileExplorerTestData.EntityId,
            _currentTenant.Id);
        file2.SetMd5("00000000000000000000000000000002");
        file2.SetReferBlobName("");
        await _fileDescriptorRepository.InsertAsync(file2, autoSave: true);

        var file3 = new FileDescriptor(
            _guidGenerator.Create(),
            _fileExplorerTestData.ContainerName3,
            _fileExplorerTestData.BlobName3,
            "testBlobName3.txt",
            "text/plain",
            "",
            null,
            _fileExplorerTestData.EntityId,
            _currentTenant.Id);
        file3.SetMd5("00000000000000000000000000000003");
        file3.SetReferBlobName("");
        await _fileDescriptorRepository.InsertAsync(file3, autoSave: true);
    }
}
