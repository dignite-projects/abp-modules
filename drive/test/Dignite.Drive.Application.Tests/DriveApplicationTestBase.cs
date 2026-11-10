using Volo.Abp.Modularity;

namespace Dignite.Drive;

/* Inherit from this class for application layer tests. */
public abstract class DriveApplicationTestBase<TStartupModule> : DriveTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
