using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dignite.Drive.EntityFrameworkCore;

public static class DriveDbContextModelCreatingExtensions
{
    public static void ConfigureDrive(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<FileDescriptor>(b =>
        {
            b.ToTable(DriveDbProperties.DbTablePrefix + "FileDescriptors", DriveDbProperties.DbSchema);
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
            // A system container's address, as an arbiter (FileDescriptorManager.CreateAsync): one live descriptor per
            // tenant, container and name. Plain unique on a nullable column for the same reason as the dedup key: only
            // live descriptors of system containers carry one.
            b.HasIndex(f => f.NameKey).IsUnique();

            // A system container's address: the provider reads, overwrites and deletes by name.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.Name });
            // The dedup lookup.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.Hash });
            // "Does anything still use this BLOB" - asked on every delete.
            b.HasIndex(f => new { f.TenantId, f.ContainerName, f.BlobName });
        });

    }
}
