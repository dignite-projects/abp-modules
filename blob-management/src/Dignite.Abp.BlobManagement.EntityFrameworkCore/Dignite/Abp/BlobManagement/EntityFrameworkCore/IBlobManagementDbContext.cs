using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

[ConnectionStringName(BlobManagementDbProperties.ConnectionStringName)]
public interface IBlobManagementDbContext : IEfCoreDbContext
{
    DbSet<FileDescriptor> FileDescriptors { get; }

}
