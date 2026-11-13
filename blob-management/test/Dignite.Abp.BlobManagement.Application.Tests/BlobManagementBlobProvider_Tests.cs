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

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// An application's containers keeping their bytes in Blob Management in the same process: <c>UseBlobManagement</c>, the provider and
/// the in-process <c>IBlobManagementBlobService</c> end to end.
/// </summary>
public abstract class BlobManagementBlobProvider_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly ICurrentTenant _currentTenant;

    protected BlobManagementBlobProvider_Tests()
    {
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Round_Trip_Through_The_BlobManagement_Under_The_Applications_Name()
    {
        var bytes = BlobManagementTestFiles.Pdf("round trip " + Guid.NewGuid());
        var container = _blobContainerFactory.Create(BlobManagementTestAppContainers.Media);

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await container.SaveAsync("2026/11/report.pdf", bytes);

            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBe(bytes);

            var file = await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "2026/11/report.pdf");
            file.ShouldNotBeNull();
            file!.MimeType.ShouldBe("application/pdf");
            file.Size.ShouldBe(bytes.Length);

            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeFalse();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBeNull();
            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeFalse();
            (await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "2026/11/report.pdf")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task An_Existing_Name_Should_Fail_As_Abp_Does_Unless_Overriding()
    {
        var container = _blobContainerFactory.Create(BlobManagementTestAppContainers.Media);
        var name = Guid.NewGuid().ToString("N") + ".pdf";
        await container.SaveAsync(name, BlobManagementTestFiles.Pdf("first " + name));

        await Should.ThrowAsync<BlobAlreadyExistsException>(() =>
            container.SaveAsync(name, BlobManagementTestFiles.Pdf("second " + name)));

        var second = BlobManagementTestFiles.Pdf("second " + name);
        await container.SaveAsync(name, second, overrideExisting: true);
        (await container.GetAllBytesAsync(name)).ShouldBe(second);
    }

    [Fact]
    public async Task The_Applications_Own_Rules_Should_Run_Before_The_Bytes_Leave_It()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _blobContainerFactory.Create(BlobManagementTestAppContainers.PicturesOnly).SaveAsync("notes.pdf", BlobManagementTestFiles.Pdf("refused")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "notes.pdf")).ShouldBeNull();
    }

    [Fact]
    public async Task The_BlobManagement_Containers_Rules_Should_Run_As_Well()
    {
        var container = _blobContainerFactory.Create(BlobManagementTestAppContainers.Images);

        await container.SaveAsync("wide.png", BlobManagementTestFiles.Png(256, 128, seed: 9));

        var stored = await container.GetAllBytesAsync("wide.png");
        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, BlobManagementTestContainers.ImagesMaxWidth / 2));
    }

    [Fact]
    public async Task A_Container_That_Is_Not_Multi_Tenant_Should_Store_For_The_Host()
    {
        var bytes = BlobManagementTestFiles.Pdf("host level " + Guid.NewGuid());

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await _blobContainerFactory.Create(BlobManagementTestAppContainers.HostLevel).SaveAsync("shared.pdf", bytes);
            (await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "shared.pdf")).ShouldBeNull();
        }

        (await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "shared.pdf"))!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Application_Containers_Sharing_A_BlobManagement_Container_Share_Its_Names_And_Its_Bytes()
    {
        var bytes = BlobManagementTestFiles.Pdf("shared " + Guid.NewGuid());
        await _blobContainerFactory.Create(BlobManagementTestAppContainers.Media).SaveAsync("a.pdf", bytes);
        await _blobContainerFactory.Create(BlobManagementTestAppContainers.Documents).SaveAsync("b.pdf", bytes);

        (await _blobContainerFactory.Create(BlobManagementTestAppContainers.Documents).GetAllBytesAsync("a.pdf")).ShouldBe(bytes);
        var a = await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "a.pdf");
        var b = await FindInBlobManagementAsync(BlobManagementTestContainers.Files, "b.pdf");
        b!.BlobName.ShouldBe(a!.BlobName);
    }

    private Task<FileDescriptor?> FindInBlobManagementAsync(string driveContainer, string name)
    {
        return WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(driveContainer, name));
    }
}
