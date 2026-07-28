using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Plays the role of a downstream host's own DbContext (e.g. a CMS's <c>CmsDbContext</c>). It owns the
/// tables, the keys, the foreign keys, the indexes and the migrations; it calls the kernel's
/// <see cref="EntityTypeBuilder{TEntity}"/> extensions from inside builder blocks it opened itself -
/// the same posture ABP's Identity/CmsKit take with <c>ConfigureAbpUser()</c>.
/// <para>
/// <see cref="ReplaceDbContextAttribute"/> is what makes <c>IDbContextProvider&lt;T&gt;</c> resolve the
/// interface to this class; implementing the interface alone is not enough.
/// </para>
/// </summary>
[ReplaceDbContext(typeof(ITestFlexFieldsDbContext))]
public class TestFlexFieldsDbContext : AbpDbContext<TestFlexFieldsDbContext>, ITestFlexFieldsDbContext
{
    public DbSet<TestArticle> Articles { get; set; } = default!;

    public DbSet<TestField> Fields { get; set; } = default!;

    public DbSet<TestArticleFlexFieldIndex> ArticleFlexFieldIndexes { get; set; } = default!;

    public TestFlexFieldsDbContext(DbContextOptions<TestFlexFieldsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TestArticle>(b =>
        {
            b.ToTable("TestArticles");
            b.ConfigureByConvention();
            b.Property(x => x.Title).IsRequired().HasMaxLength(128);

            b.ConfigureFlexFieldsProperty();   // kernel: the value bag JSON column
        });

        builder.Entity<TestField>(b =>
        {
            b.ToTable("TestFields");
            b.ConfigureByConvention();

            b.ConfigureFlexField();            // kernel: the field definition columns

            b.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<TestArticleFlexFieldIndex>(b =>
        {
            b.ToTable("TestArticleFlexFieldIndexes");
            b.ConfigureByConvention();

            b.ConfigureFlexFieldIndex();       // kernel: the typed value columns

            b.HasOne<TestArticle>().WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ArticleId, x.FieldId });
        });
    }
}
