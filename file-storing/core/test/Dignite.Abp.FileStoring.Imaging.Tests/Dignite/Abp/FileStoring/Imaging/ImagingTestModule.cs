using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FileStoring.Imaging;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(DigniteAbpFileStoringImagingModule)
    )]
public class ImagingTestModule : AbpModule
{
}
