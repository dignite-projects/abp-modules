using MongoDB.Driver;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// The downstream's own context interface - the kernel names no context, so every base class here is
/// generic over this.
/// </summary>
public interface ITestFlexFieldsMongoDbContext : IAbpMongoDbContext
{
    IMongoCollection<TestArticle> Articles { get; }

    IMongoCollection<TestField> Fields { get; }
}
