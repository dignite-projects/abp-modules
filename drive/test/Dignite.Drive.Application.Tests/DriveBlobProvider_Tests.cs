using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// An application's containers keeping their bytes in the Drive in the same process: <c>UseDrive</c>, the provider and
/// the in-process <c>IDriveBlobService</c> end to end.
/// </summary>
public abstract class DriveBlobProvider_Tests<TStartupModule> : DriveApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly ICurrentTenant _currentTenant;

    protected DriveBlobProvider_Tests()
    {
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Round_Trip_Through_The_Drive_Under_The_Applications_Name()
    {
        var bytes = DriveTestFiles.Pdf("round trip " + Guid.NewGuid());
        var container = _blobContainerFactory.Create(DriveTestAppContainers.Media);

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await container.SaveAsync("2026/11/report.pdf", bytes);

            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBe(bytes);

            var file = await FindInDriveAsync(DriveTestContainers.Files, "2026/11/report.pdf");
            file.ShouldNotBeNull();
            file!.MimeType.ShouldBe("application/pdf");
            file.Size.ShouldBe(bytes.Length);

            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeFalse();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBeNull();
            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeFalse();
            (await FindInDriveAsync(DriveTestContainers.Files, "2026/11/report.pdf")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task An_Existing_Name_Should_Fail_As_Abp_Does_Unless_Overriding()
    {
        var container = _blobContainerFactory.Create(DriveTestAppContainers.Media);
        var name = Guid.NewGuid().ToString("N") + ".pdf";
        await container.SaveAsync(name, DriveTestFiles.Pdf("first " + name));

        await Should.ThrowAsync<BlobAlreadyExistsException>(() =>
            container.SaveAsync(name, DriveTestFiles.Pdf("second " + name)));

        var second = DriveTestFiles.Pdf("second " + name);
        await container.SaveAsync(name, second, overrideExisting: true);
        (await container.GetAllBytesAsync(name)).ShouldBe(second);
    }

    [Fact]
    public async Task The_Applications_Own_Rules_Should_Run_Before_The_Bytes_Leave_It()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _blobContainerFactory.Create(DriveTestAppContainers.PicturesOnly).SaveAsync("notes.pdf", DriveTestFiles.Pdf("refused")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await FindInDriveAsync(DriveTestContainers.Files, "notes.pdf")).ShouldBeNull();
    }

    [Fact]
    public async Task The_Drive_Containers_Rules_Should_Run_As_Well()
    {
        var container = _blobContainerFactory.Create(DriveTestAppContainers.Images);

        await container.SaveAsync("wide.png", DriveTestFiles.Png(256, 128, seed: 9));

        var stored = await container.GetAllBytesAsync("wide.png");
        DriveTestFiles.Measure(stored).ShouldBe((DriveTestContainers.ImagesMaxWidth, DriveTestContainers.ImagesMaxWidth / 2));
    }

    [Fact]
    public async Task A_Container_That_Is_Not_Multi_Tenant_Should_Store_For_The_Host()
    {
        var bytes = DriveTestFiles.Pdf("host level " + Guid.NewGuid());

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await _blobContainerFactory.Create(DriveTestAppContainers.HostLevel).SaveAsync("shared.pdf", bytes);
            (await FindInDriveAsync(DriveTestContainers.Files, "shared.pdf")).ShouldBeNull();
        }

        (await FindInDriveAsync(DriveTestContainers.Files, "shared.pdf"))!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Application_Containers_Sharing_A_Drive_Container_Share_Its_Names_And_Its_Bytes()
    {
        var bytes = DriveTestFiles.Pdf("shared " + Guid.NewGuid());
        await _blobContainerFactory.Create(DriveTestAppContainers.Media).SaveAsync("a.pdf", bytes);
        await _blobContainerFactory.Create(DriveTestAppContainers.Documents).SaveAsync("b.pdf", bytes);

        (await _blobContainerFactory.Create(DriveTestAppContainers.Documents).GetAllBytesAsync("a.pdf")).ShouldBe(bytes);
        var a = await FindInDriveAsync(DriveTestContainers.Files, "a.pdf");
        var b = await FindInDriveAsync(DriveTestContainers.Files, "b.pdf");
        b!.BlobName.ShouldBe(a!.BlobName);
    }

    private Task<FileDescriptor?> FindInDriveAsync(string driveContainer, string name)
    {
        return WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(driveContainer, name));
    }
}
