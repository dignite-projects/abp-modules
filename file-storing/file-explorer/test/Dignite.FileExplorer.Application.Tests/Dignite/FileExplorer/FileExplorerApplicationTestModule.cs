using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Permissions;
using NSubstitute;
using Dignite.FileExplorer.TestObjects;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.FileExplorer;

[DependsOn(
    typeof(FileExplorerApplicationModule),
    typeof(FileExplorerDomainTestModule)
    )]
public class FileExplorerApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();

        var permissionChecker = Substitute.For<IPermissionChecker>();
        permissionChecker
            .IsGrantedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), FileExplorerPermissions.Files.Management)
            .Returns(true);
        permissionChecker
            .IsGrantedAsync(FileExplorerPermissions.Files.Management)
            .Returns(true);
        context.Services.AddSingleton(permissionChecker);

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers
                .Configure<TestContainer1>(container =>
                {
                    container.SetAuthorizationConfiguration(config =>
                    {
                        config.CreateDirectoryPermissionName = "permissionName1";
                        config.CreateFilePermissionName = "permissionName2";
                        config.UpdateFilePermissionName = "permissionName2";
                        config.DeleteFilePermissionName = "permissionName2";
                        config.SetAuthorizationHandler<TestFileDescriptorEntityAuthorizationHandler>();
                    });
                });
        });
    }
}
