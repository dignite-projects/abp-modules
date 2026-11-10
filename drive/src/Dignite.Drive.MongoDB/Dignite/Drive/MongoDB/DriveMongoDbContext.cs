using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public class DriveMongoDbContext : AbpMongoDbContext, IDriveMongoDbContext
{
    public IMongoCollection<FileDescriptor> FileDescriptors => Collection<FileDescriptor>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureDrive();
    }
}
