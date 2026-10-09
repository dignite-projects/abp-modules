using Xunit;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>Runs the shared scenarios on MongoDB (embedded mongod).</summary>
[Collection(MongoTestCollection.Name)]
public class AbpNotificationsDomainModule_Tests :
    AbpNotificationsDomainModule_Tests<MongoDbDefinitionStoreInfrastructure>
{
}
