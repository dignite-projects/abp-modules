using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

[ConnectionStringName(BlobManagementDbProperties.ConnectionStringName)]
public interface IBlobManagementMongoDbContext : IAbpMongoDbContext
{
    IMongoCollection<FileDescriptor> FileDescriptors { get; }

    IMongoCollection<DirectoryDescriptor> DirectoryDescriptors { get; }

}
