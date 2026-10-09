using System;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// A test deployment whose definition collections live in a database of their own on the embedded mongod. ABP creates
/// the collections and their indexes when the first application builds its model.
/// </summary>
public sealed class MongoDbDefinitionStoreInfrastructure : SharedDefinitionStoreInfrastructure
{
    public string ConnectionString { get; } = MongoDbFixture.GetRandomConnectionString();

    public override Type ProviderModuleType => typeof(AbpNotificationsMongoDbTestModule);
}
