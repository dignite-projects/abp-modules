using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Drive.EntityFrameworkCore;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public class DriveDbContext : AbpDbContext<DriveDbContext>, IDriveDbContext
{
    public DbSet<FileDescriptor> FileDescriptors { get; set; } = default!;

    public DriveDbContext(DbContextOptions<DriveDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureDrive();
    }
}
