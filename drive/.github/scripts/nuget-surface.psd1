# Public NuGet surface of the drive module, consumed by build/smoke-test-nuget-packages.ps1.
@{
    PackageIds = @(
        'Dignite.Drive.Domain.Shared',
        'Dignite.Drive.Domain',
        'Dignite.Drive.Application.Contracts',
        'Dignite.Drive.Application',
        'Dignite.Drive.HttpApi',
        'Dignite.Drive.HttpApi.Client',
        'Dignite.Drive.EntityFrameworkCore',
        'Dignite.Drive.MongoDB',
        'Dignite.Drive.Mcp',
        'Dignite.Drive.Installer',
        'Dignite.Abp.BlobStoring.Drive'
    )
    Types = @(
        'Dignite.Drive.DriveDomainSharedModule',
        'Dignite.Drive.DriveDomainModule',
        'Dignite.Drive.DriveApplicationContractsModule',
        'Dignite.Drive.DriveApplicationModule',
        'Dignite.Drive.DriveHttpApiModule',
        'Dignite.Drive.DriveHttpApiClientModule',
        'Dignite.Drive.EntityFrameworkCore.DriveEntityFrameworkCoreModule',
        'Dignite.Drive.MongoDB.DriveMongoDbModule',
        'Dignite.Drive.Mcp.DriveMcpModule',
        'Dignite.Drive.DriveInstallerModule',
        'Dignite.Abp.BlobStoring.Drive.AbpBlobStoringDriveModule'
    )
    Label = 'Drive '
}
