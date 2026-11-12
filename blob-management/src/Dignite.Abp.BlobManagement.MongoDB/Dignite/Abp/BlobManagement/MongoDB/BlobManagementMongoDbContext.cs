using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

[ConnectionStringName(BlobManagementDbProperties.ConnectionStringName)]
public class BlobManagementMongoDbContext : AbpMongoDbContext, IBlobManagementMongoDbContext
{
    public IMongoCollection<FileDescriptor> FileDescriptors => Collection<FileDescriptor>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureBlobManagement();
    }
}
