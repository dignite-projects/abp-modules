using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Drive.EntityFrameworkCore;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public interface IDriveDbContext : IEfCoreDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * DbSet<Question> Questions { get; }
     */
}
