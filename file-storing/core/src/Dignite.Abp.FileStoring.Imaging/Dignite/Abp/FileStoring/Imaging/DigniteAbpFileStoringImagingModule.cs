using Dignite.Abp.FileStoring;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FileStoring.Imaging;

[DependsOn(
    typeof(DigniteAbpFileStoringModule),
    typeof(AbpImagingAbstractionsModule),
    typeof(AbpImagingImageSharpModule)
)]
public class DigniteAbpFileStoringImagingModule : AbpModule
{
}
