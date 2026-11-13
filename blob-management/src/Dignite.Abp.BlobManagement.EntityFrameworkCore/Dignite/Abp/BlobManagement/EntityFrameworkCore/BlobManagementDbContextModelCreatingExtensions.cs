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
            b.Property(f => f.Hash).IsRequired().HasMaxLength(FileDescriptorConsts.MaxHashLength);
            b.Property(f => f.DedupKey).HasMaxLength(FileDescriptorConsts.MaxDedupKeyLength);
            b.Property(f => f.ClientId).HasMaxLength(FileDescriptorConsts.MaxClientIdLength);

            // Content dedup's arbiter (FileDescriptorManager.CreateAsync): one live writer per tenant, container and
            // hash. A plain unique index on a nullable column, not a filtered one, so it works on every EF Core
            // provider: only the live writer of a BLOB carries a key, and rows without one do not collide (EF Core adds
            // the IS NOT NULL filter on SQL Server by itself).
            b.HasIndex(f => f.DedupKey).IsUnique();

            b.Property(f => f.NameKey).HasMaxLength(FileDescriptorConsts.MaxNameKeyLength);
            // The address of a file an application saved, as an arbiter (FileDescriptorManager.CreateAsync): one live
            // descriptor per tenant, container and name. Plain unique on a nullable column for the same reason as the
            // dedup key: only live descriptors addressed by their name carry one.
            b.HasIndex(f => f.NameKey).IsUnique();

            // The address of a file an application saved: the provider reads, overwrites and deletes by name.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.Name });
            // The dedup lookup.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.Hash });
            // "Does anything still use this BLOB" - asked on every delete.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.BlobName });
            // The listing of one place: a container's top, or a directory.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.DirectoryId });
            // "Is anything filed here" - asked on every directory delete, by directory alone.
            b.HasIndex(f => f.DirectoryId);
        });

        builder.Entity<DirectoryDescriptor>(b =>
        {
            b.ToTable(BlobManagementDbProperties.DbTablePrefix + "DirectoryDescriptors", BlobManagementDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(d => d.ContainerName).IsRequired().HasMaxLength(FileDescriptorConsts.MaxContainerNameLength);
            b.Property(d => d.Name).IsRequired().HasMaxLength(DirectoryDescriptorConsts.MaxNameLength);
            b.Property(d => d.Path).IsRequired().HasMaxLength(DirectoryDescriptorConsts.MaxPathLength);
            b.Property(d => d.NameKey).HasMaxLength(DirectoryDescriptorConsts.NameKeyLength);

            // Sibling names, as an arbiter: one live directory per tenant, container, parent and name. A plain unique
            // index on a nullable column, as the file table's keys: a deleted directory's key is cleared, and rows
            // without one do not collide.
            b.HasIndex(d => d.NameKey).IsUnique();

            // One level of a container's tree.
            b.HasIndex(d => new { d.TenantId, d.ContainerName, d.ParentId });
            b.HasIndex(d => d.ParentId);

            // A subtree is a prefix of Path (a move rewrites it).
            b.HasIndex(d => d.Path);
        });

    }
}
