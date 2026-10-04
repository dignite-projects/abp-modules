using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
using Xunit;

namespace Dignite.FileExplorer.Authorization.Tests.Files;

/// <summary>
/// GetListAsync without the management permission lists the caller's own files. A caller with no user id
/// (a client-credentials token) has none - and must not fall through to an unfiltered query.
/// </summary>
public class FileDescriptorAppService_GetList_Tests
{
    [Fact]
    public async Task GetList_ShouldReturnNothingForACallerWithoutAUser()
    {
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        var appService = CreateAppService(fileRepository, userId: null);

        var result = await appService.GetListAsync(new GetFilesInput { ContainerName = "default" });

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
        await fileRepository.DidNotReceiveWithAnyArgs().GetCountAsync(default!, default, default, default, default, default);
        await fileRepository.DidNotReceiveWithAnyArgs().GetListAsync(default!, default, default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task GetList_ShouldFilterToTheCallersOwnFiles()
    {
        var userId = Guid.NewGuid();
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        fileRepository
            .GetListAsync(default!, default, default, default, default, default, default, default, default)
            .ReturnsForAnyArgs(new List<FileDescriptor>());
        var appService = CreateAppService(fileRepository, userId);

        await appService.GetListAsync(new GetFilesInput { ContainerName = "default" });

        await fileRepository.Received().GetCountAsync(
            "default", userId, Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static FileDescriptorAppService CreateAppService(IFileDescriptorRepository fileRepository, Guid? userId)
    {
        // Every authorization check fails: in particular the caller lacks Files.Management.
        var authorizationService = Substitute.For<IAbpAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Failed());

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(userId);

        var objectMapper = Substitute.For<IObjectMapper>();
        objectMapper.Map<List<FileDescriptor>, List<FileDescriptorDto>>(Arg.Any<List<FileDescriptor>>())
            .Returns(new List<FileDescriptorDto>());

        var serviceProvider = new ServiceCollection()
            .AddSingleton(authorizationService)
            .AddSingleton<IAuthorizationService>(authorizationService)
            .AddSingleton(currentUser)
            .AddSingleton(objectMapper)
            .BuildServiceProvider();

        return new FileDescriptorAppService(
            fileRepository,
            Substitute.For<IDirectoryDescriptorRepository>(),
            Substitute.For<FileDescriptorManager>(
                fileRepository,
                Substitute.For<IBlobContainerFactory>(),
                Substitute.For<IBlobContainerConfigurationProvider>(),
                new Dignite.Abp.FileStoring.ContainerNameValidator()),
            Substitute.For<IBlobContainerFactory>(),
            Substitute.For<IBlobContainerConfigurationProvider>(),
            Substitute.For<IImageResizer>(),
            new Dignite.Abp.FileStoring.ContainerNameValidator())
        {
            LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider)
        };
    }
}
