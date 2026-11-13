using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Content;
using Volo.Abp.Guids;
using Volo.Abp.Http.Client;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The directories over HTTP, through the generated C# client proxies: a signed-in user keeps them at
/// <c>api/blob-management/directories</c>, and an application files, lists and moves files in them as a client.
/// </summary>
public class BlobManagementDirectoryApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string UiClient = "blob-ui";
    private const string Container = BlobManagementHttpApiTestModule.DocumentsContainer;

    private readonly BlobManagementDirectoryClientProxy _directories;
    private readonly BlobManagementFileClientProxy _files;

    private readonly Guid _user = Guid.NewGuid();

    public BlobManagementDirectoryApi_Tests()
    {
        _directories = GetRequiredService<BlobManagementDirectoryClientProxy>();
        _files = GetRequiredService<BlobManagementFileClientProxy>();
    }

    [Fact]
    public async Task A_User_Should_Keep_Directories_And_An_Application_The_Files_In_Them()
    {
        await GrantDirectoryPermissionsAsync(_user);

        BlobManagementDirectoryDto docs;
        BlobManagementDirectoryDto year;
        using (TestCaller.As(UiClient, _user))
        {
            docs = await _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "Docs" });
            year = await _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "2026", ParentId = docs.Id });
            year.ParentId.ShouldBe(docs.Id);

            var top = (await _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container })).Items;
            top.Single(d => d.Id == docs.Id).HasChildren.ShouldBeTrue();
            (await _directories.GetAsync(year.Id)).Name.ShouldBe("2026");
        }

        BlobManagementFileDto file;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            // A form upload carries the directory it is filed in.
            file = await _files.CreateAsync(new CreateBlobManagementFileInput
            {
                ContainerName = Container,
                DirectoryId = year.Id,
                File = new RemoteStreamContent(new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), "minutes.pdf", "application/pdf")
            });
            file.DirectoryId.ShouldBe(year.Id);

            var files = await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, DirectoryId = year.Id, Filter = "MINUTES", Sorting = "name desc" });
            files.TotalCount.ShouldBe(1);
            files.Items.Single().Id.ShouldBe(file.Id);

            (await _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = docs.Id })).DirectoryId.ShouldBe(docs.Id);

            // Directories are a user's to keep, by the permissions put on the container: a client keeps none.
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container }));
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "Mine" }));
        }

        using (TestCaller.As(UiClient, _user))
        {
            (await _directories.UpdateAsync(year.Id, new UpdateBlobManagementDirectoryInput { Name = "2027" })).Name.ShouldBe("2027");
            (await _directories.MoveAsync(year.Id, new MoveBlobManagementDirectoryInput { ParentId = null, Order = 0 })).ParentId.ShouldBeNull();

            await ShouldFailAsync(HttpStatusCode.Conflict, () => _directories.DeleteAsync(docs.Id), BlobManagementErrorCodes.DirectoryNotEmpty);
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await _files.DeleteAsync(file.Id);
        }

        using (TestCaller.As(UiClient, _user))
        {
            await _directories.DeleteAsync(docs.Id);
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _directories.GetAsync(docs.Id));
        }
    }

    [Fact]
    public async Task The_Directory_APIs_Errors_Should_Have_Their_Statuses()
    {
        await GrantDirectoryPermissionsAsync(_user);

        using (TestCaller.As(UiClient, _user))
        {
            var folder = await _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "Folder" });

            await ShouldFailAsync(HttpStatusCode.Conflict,
                () => _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "FOLDER" }),
                BlobManagementErrorCodes.DirectoryNameAlreadyExists);
            await ShouldFailAsync(HttpStatusCode.BadRequest,
                () => _directories.MoveAsync(folder.Id, new MoveBlobManagementDirectoryInput { ParentId = folder.Id }),
                BlobManagementErrorCodes.DirectoryCannotMoveIntoItself);
            // A container without directories says so.
            await ShouldFailAsync(HttpStatusCode.BadRequest,
                () => _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = BlobManagementHttpApiTestModule.OpenContainer }),
                BlobManagementErrorCodes.DirectoriesNotAllowed);
        }

        // The management permission reads the tree and changes nothing in it.
        var reader = Guid.NewGuid();
        await GrantAsync(BlobManagementPermissions.Containers.Default, reader);
        using (TestCaller.As(UiClient, reader))
        {
            (await _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container })).Items.ShouldNotBeEmpty();
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "Mine" }));
        }

        // Without a grant the container's tree is not shown.
        using (TestCaller.As(UiClient, Guid.NewGuid()))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container }));
        }
    }

    /// <summary>Seeing the tree of the documents container and every directory permission, granted on that container.</summary>
    private async Task GrantDirectoryPermissionsAsync(Guid userId)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        foreach (var permission in new[]
                 {
                     BlobManagementPermissions.Container.View,
                     BlobManagementPermissions.Container.CreateDirectories,
                     BlobManagementPermissions.Container.UpdateDirectories,
                     BlobManagementPermissions.Container.DeleteDirectories
                 })
        {
            await GetRequiredService<IResourcePermissionGrantRepository>().InsertAsync(new ResourcePermissionGrant(
                GetRequiredService<IGuidGenerator>().Create(), permission, BlobManagementResourceNames.Container, Container, "U", userId.ToString()));
        }

        await uow.CompleteAsync();
    }

    private async Task GrantAsync(string permission, Guid userId)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        await GetRequiredService<IPermissionGrantRepository>().InsertAsync(new PermissionGrant(
            GetRequiredService<IGuidGenerator>().Create(), permission, "U", userId.ToString()));
        await uow.CompleteAsync();
    }

    private static async Task ShouldFailAsync(HttpStatusCode status, Func<Task> action, string? code = null)
    {
        var exception = await Should.ThrowAsync<AbpRemoteCallException>(action);
        exception.HttpStatusCode.ShouldBe((int)status);
        if (code != null)
        {
            exception.Error.ShouldNotBeNull().Code.ShouldBe(code);
        }
    }
}
