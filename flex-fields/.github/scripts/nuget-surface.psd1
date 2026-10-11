# Public NuGet surface of the flex-fields module, consumed by build/smoke-test-nuget-packages.ps1.
@{
    PackageIds = @(
        'Dignite.Abp.FlexFields.Domain.Shared',
        'Dignite.Abp.FlexFields.Abstractions',
        'Dignite.Abp.FlexFields.Domain',
        'Dignite.Abp.FlexFields.EntityFrameworkCore',
        'Dignite.Abp.FlexFields.MongoDB',
        'Dignite.Abp.FlexFields.Web',
        'Dignite.Abp.FlexFields.CKEditor',
        'Dignite.Abp.FlexFields.CKEditor.Web',
        'Dignite.Abp.FlexFields.Installer'
    )
    Types = @(
        'Dignite.Abp.FlexFields.FlexFieldsDomainSharedModule',
        'Dignite.Abp.FlexFields.FlexFieldsAbstractionsModule',
        'Dignite.Abp.FlexFields.FlexFieldsDomainModule',
        'Dignite.Abp.FlexFields.EntityFrameworkCore.FlexFieldsEntityFrameworkCoreModule',
        'Dignite.Abp.FlexFields.MongoDB.FlexFieldsMongoDbModule',
        'Dignite.Abp.FlexFields.Web.FlexFieldsWebModule',
        'Dignite.Abp.FlexFields.CKEditor.FlexFieldsCKEditorModule',
        'Dignite.Abp.FlexFields.CKEditor.Web.FlexFieldsCKEditorWebModule',
        'Dignite.Abp.FlexFields.FlexFieldsInstallerModule'
    )
    Label = 'FlexFields '
}
