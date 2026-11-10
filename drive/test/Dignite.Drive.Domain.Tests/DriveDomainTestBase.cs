using Volo.Abp.Modularity;

namespace Dignite.Drive;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class DriveDomainTestBase<TStartupModule> : DriveTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
