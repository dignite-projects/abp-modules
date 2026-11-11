using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// Files in user drives through <see cref="FileDescriptorManager"/>, and the line between the two kinds of container:
/// the name-addressed operations serve system containers, the user-drive operation user drives.
/// </summary>
public abstract class UserDriveFiles_Tests<TStartupModule> : DriveDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileDescriptorManager _fileDescriptorManager;

    protected UserDriveFiles_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
    }

    [Fact]
    public async Task Names_Should_Repeat_In_A_User_Drive()
    {
        var first = await UploadAsync(DriveTestContainers.UserDrive, "minutes.pdf", "first");
        var second = await UploadAsync(DriveTestContainers.UserDrive, "minutes.pdf", "second");

        second.Id.ShouldNotBe(first.Id);
        // A user-drive name is not an address: no name key, so the unique index does not keep them apart.
        first.NameKey.ShouldBeNull();
        second.NameKey.ShouldBeNull();
        first.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task Same_Content_Should_Share_One_Blob_In_A_User_Drive()
    {
        var content = "shared content " + Guid.NewGuid();

        var first = await UploadAsync(DriveTestContainers.UserDrive, "a.pdf", content);
        var second = await UploadAsync(DriveTestContainers.UserDrive, "b.pdf", content);

        second.BlobName.ShouldBe(first.BlobName);
        second.OwnsBlob().ShouldBeFalse();
    }

    [Fact]
    public async Task The_Two_Kinds_Of_Container_Should_Not_See_Each_Other()
    {
        // The name-addressed operations do not reach a user drive...
        await ShouldBeUnknownContainerAsync(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(DriveTestFiles.Pdf());
            return await _fileDescriptorManager.CreateAsync(DriveTestContainers.UserDrive, "a.pdf", stream);
        }));
        await ShouldBeUnknownContainerAsync(() =>
            WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(DriveTestContainers.UserDrive, "a.pdf")));

        // ...and the user-drive operation does not reach a system container.
        await ShouldBeUnknownContainerAsync(() => UploadAsync(DriveTestContainers.Files, "a.pdf"));
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string name, string? content = null)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(DriveTestFiles.Pdf(content ?? Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.UploadAsync(containerName, name, stream);
        });
    }

    private static async Task ShouldBeUnknownContainerAsync(Func<Task> action)
    {
        var exception = await Should.ThrowAsync<BusinessException>(action);
        exception.Code.ShouldBe(DriveErrorCodes.ContainerNotFound);
    }
}
