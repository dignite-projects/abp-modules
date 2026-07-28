using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

[Collection(MongoTestCollection.Name)]
public abstract class FlexFieldsMongoDbTestBase : AbpIntegratedTest<FlexFieldsMongoDbTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        await action();
        await uow.CompleteAsync();
    }

    protected Task<ITestFlexFieldsMongoDbContext> GetDbContextAsync()
    {
        return GetRequiredService<IMongoDbContextProvider<ITestFlexFieldsMongoDbContext>>().GetDbContextAsync();
    }

    /// <summary>
    /// Inserts a host entity through the collection directly, so a test controls exactly what shape lands in
    /// the bag - which is the point of most of the tests here.
    /// </summary>
    protected async Task<Guid> InsertArticleAsync(Action<TestArticle> configure)
    {
        var articleId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var article = new TestArticle(articleId, "Host");
            configure(article);

            var dbContext = await GetDbContextAsync();
            await dbContext.Articles.InsertOneAsync(article);
        });

        return articleId;
    }

    /// <summary>Inserts a host entity and brings its bag into its indexable form.</summary>
    protected async Task<Guid> IndexedArticleAsync(Action<TestArticle> configure)
    {
        var articleId = await InsertArticleAsync(configure);
        await SynchronizeAsync(articleId);
        return articleId;
    }

    protected async Task SynchronizeAsync(Guid articleId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            var article = await dbContext.Articles.Find(a => a.Id == articleId).SingleAsync();

            await GetRequiredService<IFlexFieldIndexManager<TestArticle>>().SynchronizeAsync(article);
        });
    }

    /// <summary>
    /// A condition naming the field both ways, which is what a caller holding the definition would build.
    /// </summary>
    protected static FlexFieldQueryCondition Condition(
        TestArticleFieldUsage usage,
        FlexFieldQueryOperator @operator,
        string value,
        FlexFieldValueType valueType)
    {
        return new FlexFieldQueryCondition(usage.Field.Id, usage.Field.Name, @operator, value, valueType);
    }

    /// <summary>
    /// Opens its own unit of work: a MongoDB context can only be created inside one, so a helper that reads
    /// outside the caller's unit of work has to bring its own.
    /// </summary>
    protected async Task<TestArticle> GetArticleAsync(Guid articleId)
    {
        TestArticle article = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            article = await dbContext.Articles.Find(a => a.Id == articleId).SingleAsync();
        });

        return article;
    }
}
