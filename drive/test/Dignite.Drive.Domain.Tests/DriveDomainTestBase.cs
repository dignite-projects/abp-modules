using Volo.Abp.Modularity;

namespace Dignite.Drive;

/* Inherit from this class for domain layer tests, as FileDescriptorManager_Tests does. */
public abstract class DriveDomainTestBase<TStartupModule> : DriveTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
