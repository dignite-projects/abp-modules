using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Drive.EntityFrameworkCore;

[ConnectionStringName(DriveDbProperties.ConnectionStringName)]
public interface IDriveDbContext : IEfCoreDbContext
{
    DbSet<FileDescriptor> FileDescriptors { get; }
}
