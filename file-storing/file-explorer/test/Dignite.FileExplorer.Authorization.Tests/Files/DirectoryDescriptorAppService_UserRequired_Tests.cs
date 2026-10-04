using System;
using System.Threading.Tasks;
using Dignite.FileExplorer.Directories;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;
using Xunit;

namespace Dignite.FileExplorer.Authorization.Tests.Files;

/// <summary>
/// Directories are per user: a caller authenticated without one (a client-credentials token) is refused
/// with that reason, not left to dereference a missing user id.
/// </summary>
public class DirectoryDescriptorAppService_UserRequired_Tests
{
    [Fact]
    public async Task GetList_ShouldRefuseACallerWithoutAUser()
    {
        var repository = Substitute.For<IDirectoryDescriptorRepository>();

        var exception = await Should.ThrowAsync<AbpAuthorizationException>(() =>
            CreateAppService(repository).GetListAsync(new GetDirectoriesInput { ContainerName = "default" }));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.UserRequired);
        await repository.DidNotReceiveWithAnyArgs().GetAllByUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Create_ShouldRefuseACallerWithoutAUser()
    {
        var exception = await Should.ThrowAsync<AbpAuthorizationException>(() =>
            CreateAppService(Substitute.For<IDirectoryDescriptorRepository>())
                .CreateAsync(new CreateDirectoryInput { ContainerName = "default", Name = "images" }));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.UserRequired);
    }

    private static DirectoryDescriptorAppService CreateAppService(IDirectoryDescriptorRepository repository)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns((Guid?)null);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(currentUser)
            .BuildServiceProvider();

        // The guard runs before anything else is touched, so the domain manager is never reached.
        return new DirectoryDescriptorAppService(null!, repository)
        {
            LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider)
        };
    }
}
