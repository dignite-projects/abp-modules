using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public class DriveMongoDbContext : AbpMongoDbContext, IDriveMongoDbContext
{
    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureDrive();
    }
}
