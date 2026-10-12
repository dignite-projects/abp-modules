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
        'Dignite.Abp.FlexFields.AbpFlexFieldsDomainSharedModule',
        'Dignite.Abp.FlexFields.AbpFlexFieldsAbstractionsModule',
        'Dignite.Abp.FlexFields.AbpFlexFieldsDomainModule',
        'Dignite.Abp.FlexFields.EntityFrameworkCore.AbpFlexFieldsEntityFrameworkCoreModule',
        'Dignite.Abp.FlexFields.MongoDB.AbpFlexFieldsMongoDbModule',
        'Dignite.Abp.FlexFields.Web.AbpFlexFieldsWebModule',
        'Dignite.Abp.FlexFields.CKEditor.AbpFlexFieldsCKEditorModule',
        'Dignite.Abp.FlexFields.CKEditor.Web.AbpFlexFieldsCKEditorWebModule',
        'Dignite.Abp.FlexFields.AbpFlexFieldsInstallerModule'
    )
    Label = 'FlexFields '
}
