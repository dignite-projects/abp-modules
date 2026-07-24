using System.Globalization;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.FileExplorer.DirectorySafety.Tests.Localization;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(FileExplorerDomainSharedModule)
)]
public class FileExplorerLocalizationTestModule : AbpModule
{
}

public class FileExplorerLocalization_Tests : AbpIntegratedTest<FileExplorerLocalizationTestModule>
{
    [Fact]
    public void ThrownBusinessException_ShouldResolveLocalizedMessage()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var exception = Should.Throw<DirectoryNotEmptyException>(() => throw new DirectoryNotEmptyException());
            var localizer = GetRequiredService<IStringLocalizer<FileExplorerResource>>();

            localizer[exception.Code].Value.ShouldBe("The directory is not empty!");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
