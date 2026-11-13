# Public NuGet surface of the blob-management module, consumed by build/smoke-test-nuget-packages.ps1.
@{
    PackageIds = @(
        'Dignite.Abp.BlobManagement.Domain.Shared',
        'Dignite.Abp.BlobManagement.Domain',
        'Dignite.Abp.BlobManagement.Application.Contracts',
        'Dignite.Abp.BlobManagement.Application',
        'Dignite.Abp.BlobManagement.HttpApi',
        'Dignite.Abp.BlobManagement.HttpApi.Client',
        'Dignite.Abp.BlobManagement.EntityFrameworkCore',
        'Dignite.Abp.BlobManagement.MongoDB',
        'Dignite.Abp.BlobManagement.Installer',
        'Dignite.Abp.BlobStoring.BlobManagement'
    )
    Types = @(
        'Dignite.Abp.BlobManagement.BlobManagementDomainSharedModule',
        'Dignite.Abp.BlobManagement.BlobManagementDomainModule',
        'Dignite.Abp.BlobManagement.BlobManagementApplicationContractsModule',
        'Dignite.Abp.BlobManagement.BlobManagementApplicationModule',
        'Dignite.Abp.BlobManagement.BlobManagementHttpApiModule',
        'Dignite.Abp.BlobManagement.BlobManagementHttpApiClientModule',
        'Dignite.Abp.BlobManagement.EntityFrameworkCore.BlobManagementEntityFrameworkCoreModule',
        'Dignite.Abp.BlobManagement.MongoDB.BlobManagementMongoDbModule',
        'Dignite.Abp.BlobManagement.BlobManagementInstallerModule',
        'Dignite.Abp.BlobStoring.BlobManagement.AbpBlobStoringBlobManagementModule'
    )
    Label = 'BlobManagement '
}
