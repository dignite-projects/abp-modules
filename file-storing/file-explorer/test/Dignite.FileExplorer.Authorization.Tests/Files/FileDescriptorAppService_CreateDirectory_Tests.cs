using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
using Xunit;

namespace Dignite.FileExplorer.Authorization.Tests.Files;

/// <summary>
/// A new file may only be put in a directory that exists and shares its container, owner and tenant - the
/// same rule a move already had to follow in UpdateAsync.
/// </summary>
public class FileDescriptorAppService_CreateDirectory_Tests
{
    private const string Container = "default";

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IDirectoryDescriptorRepository _directoryRepository = Substitute.For<IDirectoryDescriptorRepository>();
    private readonly IFileDescriptorRepository _fileRepository = Substitute.For<IFileDescriptorRepository>();
    private readonly FileDescriptorManager _fileManager;

    public FileDescriptorAppService_CreateDirectory_Tests()
    {
        _fileManager = Substitute.For<FileDescriptorManager>(
            _fileRepository,
            Substitute.For<IBlobContainerFactory>(),
            Substitute.For<IBlobContainerConfigurationProvider>(),
            new Dignite.Abp.FileStoring.ContainerNameValidator());
        _fileManager
            .CreateAsync(default(string)!, default(IRemoteStreamContent)!, default(string)!, default(Guid?), default(string)!, default)
            .ReturnsForAnyArgs(new FileDescriptor(Guid.NewGuid(), Container, "blob", "a.png", "image/png", null, null, null, _tenantId));
    }

    [Fact]
    public async Task Create_ShouldRejectADirectoryInAnotherContainer()
    {
        var directoryId = ArrangeDirectory("other-container", _userId, _tenantId);

        await ShouldBeRejectedAsync(directoryId);
    }

    [Fact]
    public async Task Create_ShouldRejectAnotherUsersDirectory()
    {
        var directoryId = ArrangeDirectory(Container, Guid.NewGuid(), _tenantId);

        await ShouldBeRejectedAsync(directoryId);
    }

    [Fact]
    public async Task Create_ShouldRejectAnotherTenantsDirectory()
    {
        var directoryId = ArrangeDirectory(Container, _userId, Guid.NewGuid());

        await ShouldBeRejectedAsync(directoryId);
    }

    [Fact]
    public async Task Create_ShouldRejectADirectoryThatDoesNotExist()
    {
        await ShouldBeRejectedAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task Create_ShouldStoreTheFileInTheCallersOwnDirectory()
    {
        var directoryId = ArrangeDirectory(Container, _userId, _tenantId);

        await CreateAppService().CreateAsync(Input(directoryId));

        await _fileManager.Received().CreateAsync(
            Container, Arg.Any<IRemoteStreamContent>(), Arg.Any<string>(), directoryId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private async Task ShouldBeRejectedAsync(Guid directoryId)
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => CreateAppService().CreateAsync(Input(directoryId)));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.DirectoryNotExist);
        // Refused before anything is written to blob storage.
        await _fileManager.DidNotReceiveWithAnyArgs().CreateAsync(
            default(string)!, default(IRemoteStreamContent)!, default(string)!, default(Guid?), default(string)!, default);
    }

    private Guid ArrangeDirectory(string containerName, Guid creatorId, Guid tenantId)
    {
        var directory = new DirectoryDescriptor(Guid.NewGuid(), containerName, "images", null, 0, tenantId)
        {
            CreatorId = creatorId
        };
        _directoryRepository.FindAsync(directory.Id, false, Arg.Any<CancellationToken>()).Returns(directory);
        return directory.Id;
    }

    private static CreateFileInput Input(Guid directoryId)
    {
        return new CreateFileInput
        {
            ContainerName = Container,
            DirectoryId = directoryId,
            File = new RemoteStreamContent(new MemoryStream(new byte[] { 1, 2, 3 }), "a.png", "image/png")
        };
    }

    private FileDescriptorAppService CreateAppService()
    {
        var authorizationService = Substitute.For<IAbpAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(_userId);
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(_tenantId);

        var objectMapper = Substitute.For<IObjectMapper>();
        objectMapper.Map<FileDescriptor, FileDescriptorDto>(Arg.Any<FileDescriptor>()).Returns(new FileDescriptorDto());

        var serviceProvider = new ServiceCollection()
            .AddSingleton(authorizationService)
            .AddSingleton<IAuthorizationService>(authorizationService)
            .AddSingleton(currentUser)
            .AddSingleton(currentTenant)
            .AddSingleton(objectMapper)
            .BuildServiceProvider();

        return new FileDescriptorAppService(
            _fileRepository,
            _directoryRepository,
            _fileManager,
            Substitute.For<IBlobContainerFactory>(),
            Substitute.For<IBlobContainerConfigurationProvider>(),
            Substitute.For<IImageResizer>(),
            new Dignite.Abp.FileStoring.ContainerNameValidator())
        {
            LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider)
        };
    }
}
