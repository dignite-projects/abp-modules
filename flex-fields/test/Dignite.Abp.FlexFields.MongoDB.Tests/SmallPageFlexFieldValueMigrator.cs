using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// A test-only subclass with a tiny page size, so migration tests cross page boundaries without seeding
/// hundreds of entities. Note what is <i>not</i> here: the migrator itself is the kernel's, unchanged - the
/// MongoDB package ships no migrator, and this subclass exists only to shrink the page size.
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
