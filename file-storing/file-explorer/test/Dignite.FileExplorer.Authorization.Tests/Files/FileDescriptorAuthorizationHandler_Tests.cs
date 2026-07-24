using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.FileExplorer.Authorization.Tests.Files;

public class FileDescriptorAuthorizationHandler_Tests
{
    [Fact]
    public async Task Create_ShouldBeDeniedWithoutCreatePermission()
    {
        var handler = CreateHandler();
        var resource = CreateFile();
        var context = new AuthorizationHandlerContext(
            new[] { CommonOperations.Create },
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);

        await ((IAuthorizationHandler)handler).HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ShouldBeDeniedWithoutDeletePermissionForNonOwner()
    {
        var handler = CreateHandler();
        var resource = CreateFile();
        resource.CreatorId = Guid.NewGuid();
        var context = new AuthorizationHandlerContext(
            new[] { CommonOperations.Delete },
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);

        await ((IAuthorizationHandler)handler).HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_ShouldBeDeniedWithoutGetPermission()
    {
        var handler = CreateHandler();
        var resource = CreateFile();
        var context = new AuthorizationHandlerContext(
            new[] { CommonOperations.Get },
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);

        await ((IAuthorizationHandler)handler).HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    private static FileDescriptorAuthorizationHandler CreateHandler()
    {
        var configuration = new BlobContainerConfiguration();
        configuration.SetAuthorizationConfiguration(options =>
        {
            options.CreateFilePermissionName = "Files.Create";
            options.DeleteFilePermissionName = "Files.Delete";
            options.GetFilePermissionName = "Files.Get";
        });

        var configurationProvider = Substitute.For<IBlobContainerConfigurationProvider>();
        configurationProvider.Get("Default").Returns(configuration);

        var permissionChecker = Substitute.For<IPermissionChecker>();
        permissionChecker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>()).Returns(false);

        return new FileDescriptorAuthorizationHandler(
            new ServiceProviderStub(),
            permissionChecker,
            configurationProvider,
            Substitute.For<IDirectoryDescriptorRepository>());
    }

    private static FileDescriptor CreateFile()
    {
        return new FileDescriptor(
            Guid.NewGuid(),
            "Default",
            "blob-name",
            "file-name",
            "text/plain",
            string.Empty,
            null,
            string.Empty,
            null);
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
