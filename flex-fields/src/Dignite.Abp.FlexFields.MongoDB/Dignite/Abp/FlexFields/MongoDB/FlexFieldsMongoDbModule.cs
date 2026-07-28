using MongoDB.Bson.Serialization;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Registers no services - deliberately, exactly like <c>FlexFieldsEntityFrameworkCoreModule</c>. This
/// package ships no <c>MongoDbContext</c>, no collection and no repository: a downstream host owns its
/// context and its collections, and reaches the kernel only through the abstract base classes here and
/// <see cref="FlexFieldsMongoDbContextExtensions"/>. The dependency declarations exist so
/// <c>FlexFieldConsts</c> and the field-type registrations are in the graph.
/// <para>
/// Contrast a module that owns entities (this repository's <c>FileExplorerMongoDbModule</c>, say), which
/// calls <c>AddMongoDbContext</c> and registers repositories. FlexFields has no
/// <c>ConnectionStringName</c> and no collection-name prefix, because it has nothing to name.
/// </para>
/// <para>
/// It does settle how the kernel's own dictionary types serialize, which is not service registration and is
/// not ownership of anyone's data - it is the same thing the EF Core provider does by attaching a JSON value
/// converter to the bag column. Done in a static constructor, and only for the kernel's own types, following
/// <c>AbpMongoDbModule</c>'s own Guid registration.
/// </para>
/// </summary>
[DependsOn(
    typeof(FlexFieldsDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class FlexFieldsMongoDbModule : AbpModule
{
    static FlexFieldsMongoDbModule()
    {
        // TryRegister: a host may create several ABP applications in one process (every integration test
        // class does), and BSON serializer registration is per-process, not per-application.
        BsonSerializer.TryRegisterSerializer(new FlexFieldValueBagSerializer<FlexFieldDictionary>());
        BsonSerializer.TryRegisterSerializer(new FlexFieldValueBagSerializer<FieldConfigurationDictionary>());
    }
}
