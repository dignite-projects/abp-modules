using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
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
using Xunit;

namespace Dignite.FileExplorer.Update.Tests.Files;

public class FileDescriptorAppService_Tests
{
    [Fact]
    public void UpdateInput_ShouldTrackExplicitNullValues()
    {
        var input = JsonSerializer.Deserialize<UpdateFileInput>(
            "{\"cellName\":null,\"directoryId\":null}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        input.ShouldNotBeNull();
        input.CellNameSpecified.ShouldBeTrue();
        input.DirectoryIdSpecified.ShouldBeTrue();
    }

    [Fact]
    public async Task Rename_ShouldPreserveDirectoryAndCellName()
    {
        var directoryId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var file = new FileDescriptor(
            Guid.NewGuid(),
            "Default",
            "blob-name",
            "old-name.txt",
            "text/plain",
            "thumbnail",
            directoryId,
            "entity-id",
            tenantId)
        {
            CreatorId = creatorId
        };
        var directory = new DirectoryDescriptor(
            directoryId,
            "Default",
            "directory",
            null,
            0,
            tenantId)
        {
            CreatorId = creatorId
        };

        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        fileRepository.GetAsync(file.Id).Returns(file);
        fileRepository.UpdateAsync(file).Returns(file);

        var directoryRepository = Substitute.For<IDirectoryDescriptorRepository>();
        directoryRepository.FindAsync(directoryId, false).Returns(directory);

        var fileManager = Substitute.For<FileDescriptorManager>(
            fileRepository,
            Substitute.For<IBlobContainerFactory>(),
            Substitute.For<IBlobContainerConfigurationProvider>(),
            new Dignite.Abp.FileStoring.ContainerNameValidator());
        fileManager.ValidateAsync(file).Returns(Task.CompletedTask);

        var authorizationService = Substitute.For<IAbpAuthorizationService>();
        authorizationService.AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());

        var objectMapper = Substitute.For<IObjectMapper>();
        objectMapper.Map<FileDescriptor, FileDescriptorDto>(file).Returns(new FileDescriptorDto());

        var serviceProvider = new ServiceCollection()
            .AddSingleton(authorizationService)
            .AddSingleton<IAuthorizationService>(authorizationService)
            .AddSingleton<IObjectMapper>(objectMapper)
            .BuildServiceProvider();

        var appService = new FileDescriptorAppService(
            fileRepository,
            directoryRepository,
            fileManager,
            Substitute.For<IBlobContainerFactory>(),
            Substitute.For<IBlobContainerConfigurationProvider>(),
            Substitute.For<IImageResizer>())
        {
            LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider)
        };

        await appService.UpdateAsync(file.Id, new UpdateFileInput { Name = "new-name.txt" });

        file.Name.ShouldBe("new-name.txt");
        file.DirectoryId.ShouldBe(directoryId);
        file.CellName.ShouldBe("thumbnail");
    }
}
