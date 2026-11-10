using Volo.Abp.Modularity;

namespace Dignite.Drive;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class DriveApplicationTestBase<TStartupModule> : DriveTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
