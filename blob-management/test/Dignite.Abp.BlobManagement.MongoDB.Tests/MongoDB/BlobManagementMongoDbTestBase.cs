namespace Dignite.Abp.BlobManagement.MongoDB;

/* A base class for MongoDB-specific integration tests; provider-agnostic scenarios live in
 * Domain.Tests / Application.Tests as abstract classes, inherited here per provider.
 */
public abstract class BlobManagementMongoDbTestBase : BlobManagementTestBase<BlobManagementMongoDbTestModule>
{

}
