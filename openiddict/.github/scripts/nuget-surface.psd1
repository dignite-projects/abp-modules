# Public NuGet surface of the openiddict tree, consumed by build/smoke-test-nuget-packages.ps1.
@{
    PackageIds = @(
        'Dignite.Abp.OpenIddict.ClientIdMetadataDocuments'
    )
    Types = @(
        'Dignite.Abp.OpenIddict.ClientIdMetadataDocuments.AbpOpenIddictClientIdMetadataDocumentsModule',
        'Dignite.Abp.OpenIddict.ClientIdMetadataDocuments.AbpClientIdMetadataDocumentOptions',
        'Dignite.Abp.OpenIddict.ClientIdMetadataDocuments.ClientIdMetadataDocumentClientProvisioner'
    )
}
