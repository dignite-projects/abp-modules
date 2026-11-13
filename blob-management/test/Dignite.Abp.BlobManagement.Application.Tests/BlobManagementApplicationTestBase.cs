using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement;

/* Inherit from this class for application layer tests. */
public abstract class BlobManagementApplicationTestBase<TStartupModule> : BlobManagementTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
