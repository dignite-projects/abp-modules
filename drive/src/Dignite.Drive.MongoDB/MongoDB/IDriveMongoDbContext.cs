using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public interface IDriveMongoDbContext : IAbpMongoDbContext
{
    /* Define mongo collections here. Example:
     * IMongoCollection<Question> Questions { get; }
     */
}
