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
/// The directories and the file library over HTTP: a signed-in user calls <c>api/blob-management/directories</c> and
/// the listing, renaming and moving of <c>/files</c> through the generated C# client proxies.
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
    public async Task A_User_Should_Manage_Directories_And_The_Files_In_Them_Through_The_Proxies()
    {
        foreach (var permission in BlobManagementPermissions.GetAll())
        {
            await GrantAsync(permission, _user);
        }

        using (TestCaller.As(UiClient, _user))
        {
            var docs = await _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "Docs" });
            var year = await _directories.CreateAsync(new CreateBlobManagementDirectoryInput { ContainerName = Container, Name = "2026", ParentId = docs.Id });
            year.ParentId.ShouldBe(docs.Id);

            var top = (await _directories.GetListAsync(new GetBlobManagementDirectoriesInput { ContainerName = Container })).Items;
            var listed = top.Single(d => d.Id == docs.Id);
            listed.HasChildren.ShouldBeTrue();
            listed.Access.UploadFiles.ShouldBeTrue();
            (await _directories.GetAsync(year.Id)).CanDelete.ShouldBeTrue();

            // A form upload carries the directory it is filed in.
            var file = await _files.CreateAsync(new CreateBlobManagementFileInput
            {
                ContainerName = Container,
                DirectoryId = year.Id,
                File = new RemoteStreamContent(new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), "minutes.pdf", "application/pdf")
            });
            file.DirectoryId.ShouldBe(year.Id);

            var files = await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, DirectoryId = year.Id, Filter = "MINUTES", Sorting = "name desc" });
            files.TotalCount.ShouldBe(1);
            files.Items.Single().Id.ShouldBe(file.Id);

            (await _files.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "final.pdf" })).Name.ShouldBe("final.pdf");
            (await _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = docs.Id })).DirectoryId.ShouldBe(docs.Id);

            (await _directories.UpdateAsync(year.Id, new UpdateBlobManagementDirectoryInput { Name = "2027" })).Name.ShouldBe("2027");
            (await _directories.RestrictAsync(year.Id, new RestrictBlobManagementDirectoryInput { IsRestricted = true })).IsRestricted.ShouldBeTrue();
            (await _directories.MoveAsync(year.Id, new MoveBlobManagementDirectoryInput { ParentId = null, Order = 0 })).ParentId.ShouldBeNull();

            await ShouldFailAsync(HttpStatusCode.Conflict, () => _directories.DeleteAsync(docs.Id), BlobManagementErrorCodes.DirectoryNotEmpty);
            await _files.DeleteAsync(file.Id);
            await _directories.DeleteAsync(docs.Id);
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _directories.GetAsync(docs.Id));
        }
    }

    [Fact]
    public async Task The_Directory_APIs_Errors_Should_Have_Their_Statuses()
    {
        foreach (var permission in BlobManagementPermissions.GetAll())
        {
            await GrantAsync(permission, _user);
        }

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
            await ShouldFailAsync(HttpStatusCode.BadRequest,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Sorting = "Hash" }),
                BlobManagementErrorCodes.SortingNotSupported);
        }

        // Without a grant the container's files are not listed.
        using (TestCaller.As(UiClient, Guid.NewGuid()))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container }));
        }
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
