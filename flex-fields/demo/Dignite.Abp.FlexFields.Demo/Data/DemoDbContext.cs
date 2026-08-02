using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.FileExplorer.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Data;

// [ReplaceDbContext] is what makes IDbContextProvider<IDemoDbContext> resolve to this class - the
// FlexFields kernel's EF Core base classes (index manager, query executor, field repository) all depend
// on that provider. Implementing IDemoDbContext alone is not enough; without this attribute they fail to
// resolve at startup.
[ReplaceDbContext(typeof(IDemoDbContext))]
public class DemoDbContext : AbpDbContext<DemoDbContext>, IDemoDbContext
{

    public const string DbTablePrefix = "App";
    public const string DbSchema = null;

    public DbSet<Product> Products { get; set; } = default!;

    public DbSet<ProductField> ProductFields { get; set; } = default!;

    public DbSet<ProductFlexFieldIndex> ProductFlexFieldIndexes { get; set; } = default!;

    public DemoDbContext(DbContextOptions<DemoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigurePermissionManagement();
        builder.ConfigureBlobStoring();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        // Backs the FileExplorer bolt-on field type - see the csproj comment for why this demo
        // (uniquely) references another module tree's EF Core model.
        builder.ConfigureFileExplorer();

        /* Configure your own entities here */

        builder.Entity<Product>(b =>
        {
            b.ToTable(DbTablePrefix + "Products");
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);

            b.ConfigureFlexFieldsProperty(); // kernel: the value bag JSON column
        });

        builder.Entity<ProductField>(b =>
        {
            b.ToTable(DbTablePrefix + "ProductFields");
            b.ConfigureByConvention();

            b.ConfigureFlexField(); // kernel: the field definition columns (Name/DisplayName/Description/FieldTypeName/Configuration)

            // Required/Searchable are this demo's own columns, not the kernel's - see the remarks on
            // ProductField for why they live on the definition here instead of a separate usage record.
            // ConfigureByConvention() above already maps them; nothing further to configure.

            // Uniqueness is the downstream's call, not the kernel's - it deliberately adds no index here.
            b.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<ProductFlexFieldIndex>(b =>
        {
            b.ToTable(DbTablePrefix + "ProductFlexFieldIndexes");
            b.ConfigureByConvention();

            b.ConfigureFlexFieldIndex(); // kernel: the seven typed value columns

            // The kernel maps no relationship to the host - this FK and its cascade delete are ours.
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ProductId, x.FieldId });
        });
    }
}

