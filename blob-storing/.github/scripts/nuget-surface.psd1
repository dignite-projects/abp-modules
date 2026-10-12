# Public NuGet surface of the blob-storing module, consumed by build/smoke-test-nuget-packages.ps1.
@{
    PackageIds = @(
        'Dignite.Abp.BlobStoring.Pipeline',
        'Dignite.Abp.BlobStoring.Imaging'
    )
    Types = @(
        'Dignite.Abp.BlobStoring.Pipeline.AbpBlobStoringPipelineModule',
        'Dignite.Abp.BlobStoring.Pipeline.MaxSizeContributor',
        'Dignite.Abp.BlobStoring.Pipeline.AllowedContentTypesContributor',
        'Dignite.Abp.BlobStoring.Pipeline.GZipContributor',
        'Dignite.Abp.BlobStoring.Pipeline.IMimeTypeDetector',
        'Dignite.Abp.BlobStoring.Pipeline.BlobContainerConfigurationExtensions',
        'Dignite.Abp.BlobStoring.Imaging.AbpBlobStoringImagingModule',
        'Dignite.Abp.BlobStoring.Imaging.ImageResizeContributor',
        'Dignite.Abp.BlobStoring.Imaging.ImageCompressContributor',
        'Dignite.Abp.BlobStoring.Imaging.ImageDecodeGuardConfiguration',
        'Dignite.Abp.BlobStoring.Imaging.ImagingBlobContainerConfigurationExtensions'
    )
}
