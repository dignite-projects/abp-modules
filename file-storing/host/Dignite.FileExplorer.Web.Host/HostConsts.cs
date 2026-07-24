using Volo.Abp.Identity;

namespace Dignite.FileExplorer.Web.Host;

public static class HostConsts
{
    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
    public const string AdminPasswordConfigurationKey = "Identity:AdminPassword";
}
