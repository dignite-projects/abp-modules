namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

/* A base class for EF Core-specific integration tests; provider-agnostic scenarios live in
 * Domain.Tests / Application.Tests as abstract classes, inherited here per provider.
 */
public abstract class BlobManagementEntityFrameworkCoreTestBase : BlobManagementTestBase<BlobManagementEntityFrameworkCoreTestModule>
{

}
