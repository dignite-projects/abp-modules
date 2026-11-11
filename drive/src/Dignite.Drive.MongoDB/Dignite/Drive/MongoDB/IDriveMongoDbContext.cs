using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public interface IDriveMongoDbContext : IAbpMongoDbContext
{
    IMongoCollection<FileDescriptor> FileDescriptors { get; }

}
