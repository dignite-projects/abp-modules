using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement;

/* Inherit from this class for domain layer tests, as FileDescriptorManager_Tests does. */
public abstract class BlobManagementDomainTestBase<TStartupModule> : BlobManagementTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
