using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dignite.Abp.BlobManagement.EntityFrameworkCore;

public static class BlobManagementDbContextModelCreatingExtensions
{
    public static void ConfigureBlobManagement(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<FileDescriptor>(b =>
        {
            b.ToTable(BlobManagementDbProperties.DbTablePrefix + "FileDescriptors", BlobManagementDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(f => f.ContainerName).IsRequired().HasMaxLength(FileDescriptorConsts.MaxContainerNameLength);
            b.Property(f => f.Name).IsRequired().HasMaxLength(FileDescriptorConsts.MaxNameLength);
            b.Property(f => f.BlobName).IsRequired().HasMaxLength(FileDescriptorConsts.MaxBlobNameLength);
            b.Property(f => f.MimeType).IsRequired().HasMaxLength(FileDescriptorConsts.MaxMimeTypeLength);
            // Only a file stored with a checksum to verify has one.
            b.Property(f => f.Hash).HasMaxLength(FileDescriptorConsts.MaxHashLength);
            b.Property(f => f.ClientId).HasMaxLength(FileDescriptorConsts.MaxClientIdLength);

            b.Property(f => f.NameKey).HasMaxLength(FileDescriptorConsts.MaxNameKeyLength);
            // The address of a file an application saved, and the arbiter of it (FileDescriptorManager.CreateAsync): one
            // descriptor per tenant, container and name. The provider reads, overwrites and deletes by it. A plain
            // unique index on a nullable column, not a filtered one, so it works on every EF Core provider: a file kept
            // by id has no key, and rows without one do not collide (EF Core adds the IS NOT NULL filter on SQL Server
            // by itself).
            b.HasIndex(f => f.NameKey).IsUnique();

            // The public address of a file kept by id: its BLOB.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.BlobName });
            // The listing of one place - a container's top, or a directory - sorted by name.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.DirectoryId, f.Name });
            // "Is anything filed here" - asked on every directory delete, by directory alone.
            b.HasIndex(f => f.DirectoryId);
            // The pending files of a tenant uploaded before a time, oldest first: the cleanup of abandoned uploads.
            b.HasIndex(f => new { f.TenantId, f.IsPending, f.CreationTime });
        });

        builder.Entity<DirectoryDescriptor>(b =>
        {
            b.ToTable(BlobManagementDbProperties.DbTablePrefix + "DirectoryDescriptors", BlobManagementDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(d => d.ContainerName).IsRequired().HasMaxLength(FileDescriptorConsts.MaxContainerNameLength);
            b.Property(d => d.Name).IsRequired().HasMaxLength(DirectoryDescriptorConsts.MaxNameLength);
            b.Property(d => d.Path).IsRequired().HasMaxLength(DirectoryDescriptorConsts.MaxPathLength);
            b.Property(d => d.NameKey).IsRequired().HasMaxLength(DirectoryDescriptorConsts.NameKeyLength);

            // Sibling names, as an arbiter: one directory per tenant, container, parent and name. Every directory holds
            // its key; a deleted one is gone with it.
            b.HasIndex(d => d.NameKey).IsUnique();

            // One level of a container's tree.
            b.HasIndex(d => new { d.TenantId, d.ContainerName, d.ParentId });
            b.HasIndex(d => d.ParentId);

            // A subtree is a prefix of Path (a move rewrites it).
            b.HasIndex(d => d.Path);
        });

    }
}
