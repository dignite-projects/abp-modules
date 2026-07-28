using MongoDB.Driver;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// The downstream's own context: it owns the collections and their names, and calls the kernel's index
/// helper for the one index the kernel's contracts need. The kernel contributes no collection of its own -
/// this is the MongoDB counterpart of the EF Core test context's three <c>ConfigureFlexField*()</c> calls,
/// and it is deliberately shorter, because there is no pivot table to configure.
/// </summary>
public class TestFlexFieldsMongoDbContext : AbpMongoDbContext, ITestFlexFieldsMongoDbContext
{
    public IMongoCollection<TestArticle> Articles => Collection<TestArticle>();

    public IMongoCollection<TestField> Fields => Collection<TestField>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.Entity<TestArticle>(b =>
        {
            b.CollectionName = "TestArticles";
            b.ConfigureIndexes(indexes => indexes.ConfigureFlexFieldsIndex());
        });

        modelBuilder.Entity<TestField>(b =>
        {
            b.CollectionName = "TestFields";
        });
    }
}
