using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// A test-only subclass with a tiny page size, so migration tests cross page boundaries without seeding
/// hundreds of entities. Registered as itself rather than as
/// <see cref="IFlexFieldValueMigrator{TEntity}"/>, which stays bound to the kernel's open-generic
/// registration - a downstream needs no subclass of its own, and the other tests here prove it by resolving
/// the interface directly.
/// </summary>
public class SmallPageFlexFieldValueMigrator : FlexFieldValueMigrator<TestArticle>
{
    protected override int MigrationPageSize => 2;

    public SmallPageFlexFieldValueMigrator(
        IFlexFieldProvider<TestArticle> flexFieldProvider,
        IBasicRepository<TestArticle> repository,
        IUnitOfWorkManager unitOfWorkManager)
        : base(flexFieldProvider, repository, unitOfWorkManager)
    {
    }
}
