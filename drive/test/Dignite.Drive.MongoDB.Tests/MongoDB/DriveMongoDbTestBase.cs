namespace Dignite.Drive.MongoDB;

/* A base class for MongoDB-specific integration tests; provider-agnostic scenarios live in
 * Domain.Tests / Application.Tests as abstract classes, inherited here per provider.
 */
public abstract class DriveMongoDbTestBase : DriveTestBase<DriveMongoDbTestModule>
{

}
