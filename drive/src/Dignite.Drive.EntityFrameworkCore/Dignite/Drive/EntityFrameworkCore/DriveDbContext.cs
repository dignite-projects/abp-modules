using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Drive.EntityFrameworkCore;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public class DriveDbContext : AbpDbContext<DriveDbContext>, IDriveDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * public DbSet<Question> Questions { get; set; }
     */

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
