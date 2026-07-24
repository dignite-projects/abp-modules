using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dignite.FileExplorer.EntityFrameworkCore;

public static class FileExplorerDbContextModelCreatingExtensions
{
    public static void ConfigureFileExplorer(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<DirectoryDescriptor>(b =>
        {
            //Configure table & schema name
            b.ToTable(FileExplorerDbProperties.DbTablePrefix + "DirectoryDescriptors", FileExplorerDbProperties.DbSchema);

            b.ConfigureByConvention();

            //Properties
            b.Property(q => q.ContainerName).IsRequired().HasMaxLength(FileConsts.MaxContainerNameLength);
            b.Property(q => q.Name).IsRequired().HasMaxLength(DirectoryDescriptorConsts.MaxNameLength);

            //Indexes
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.CreatorId, q.ParentId });

            b.HasOne<DirectoryDescriptor>()
                .WithMany()
                .HasForeignKey(q => q.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<FileDescriptor>(b =>
        {
            //Configure table & schema name
            b.ToTable(FileExplorerDbProperties.DbTablePrefix + "FileDescriptors", FileExplorerDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(q => q.ContainerName).IsRequired().HasMaxLength(FileConsts.MaxContainerNameLength);
            b.Property(q => q.BlobName).IsRequired().HasMaxLength(FileConsts.MaxBlobNameLength);
            b.Property(q => q.Name).HasMaxLength(FileConsts.MaxNameLength);
            b.Property(q => q.MimeType).HasMaxLength(FileConsts.MaxMimeTypeLength);
            b.Property(q => q.Md5).HasMaxLength(FileConsts.MaxMd5Length);
            b.Property(q => q.ReferBlobName).HasMaxLength(FileConsts.MaxBlobNameLength);

            //Properties
            b.Property(q => q.CellName).HasMaxLength(FileDescriptorConsts.MaxCellNameLength);
            b.Property(q => q.EntityId).HasMaxLength(FileDescriptorConsts.MaxEntityIdLength);

            //Indexes
            // SQL treats NULL values as distinct in a unique composite index. Keep
            // host records in a separate index without TenantId so host-level names
            // and hashes are unique as well as tenant-level records.
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.BlobName })
                .IsUnique()
                .HasFilter($"{nameof(FileDescriptor.TenantId)} IS NOT NULL");
            b.HasIndex(q => new { q.ContainerName, q.BlobName })
                .IsUnique()
                .HasFilter($"{nameof(FileDescriptor.TenantId)} IS NULL");
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.Md5 })
                .IsUnique()
                .HasFilter($"{nameof(FileDescriptor.TenantId)} IS NOT NULL AND {nameof(FileDescriptor.Md5)} <> ''");
            b.HasIndex(q => new { q.ContainerName, q.Md5 })
                .IsUnique()
                .HasFilter($"{nameof(FileDescriptor.TenantId)} IS NULL AND {nameof(FileDescriptor.Md5)} <> ''");
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.ReferBlobName });
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.EntityId });
            b.HasIndex(q => new { q.TenantId, q.ContainerName,q.CreationTime, q.CreatorId, q.DirectoryId });

            b.HasOne<DirectoryDescriptor>()
                .WithMany()
                .HasForeignKey(q => q.DirectoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ApplyObjectExtensionMappings();
        });
    }
}
