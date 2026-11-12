using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

[ConnectionStringName(BlobManagementDbProperties.ConnectionStringName)]
public class BlobManagementDbContext : AbpDbContext<BlobManagementDbContext>, IBlobManagementDbContext
{
    public DbSet<FileDescriptor> FileDescriptors { get; set; } = default!;

    public BlobManagementDbContext(DbContextOptions<BlobManagementDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureBlobManagement();
    }
}
