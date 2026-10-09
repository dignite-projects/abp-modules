using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore;

public static class NotificationDefinitionStoreDbContextModelCreatingExtensions
{
    /// <summary>
    /// Maps <c>NotifDefinitionGroups</c> and <c>NotifDefinitions</c> (prefix and schema from
    /// <see cref="NotificationDefinitionStoreDbProperties"/>). Call it from the host's migration DbContext next to
    /// <c>ConfigureNotificationCenter()</c>. Definitions are host-level, so a tenant-only database gets no tables — the
    /// same rule as ABP's feature definition tables.
    /// </summary>
    public static void ConfigureNotificationDefinitionStore(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        if (builder.IsTenantOnlyDatabase())
        {
            return;
        }

        builder.Entity<NotificationGroupDefinitionRecord>(b =>
        {
            b.ToTable(
                NotificationDefinitionStoreDbProperties.DbTablePrefix + "DefinitionGroups",
                NotificationDefinitionStoreDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(NotificationGroupDefinitionRecordConsts.MaxNameLength).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(NotificationGroupDefinitionRecordConsts.MaxDisplayNameLength)
                .IsRequired();

            b.HasIndex(x => new { x.Name }).IsUnique();

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<NotificationDefinitionRecord>(b =>
        {
            b.ToTable(
                NotificationDefinitionStoreDbProperties.DbTablePrefix + "Definitions",
                NotificationDefinitionStoreDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(x => x.GroupName).HasMaxLength(NotificationGroupDefinitionRecordConsts.MaxNameLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(NotificationDefinitionRecordConsts.MaxNameLength).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(NotificationDefinitionRecordConsts.MaxDisplayNameLength)
                .IsRequired();
            b.Property(x => x.Description).HasMaxLength(NotificationDefinitionRecordConsts.MaxDescriptionLength);
            b.Property(x => x.PermissionName).HasMaxLength(NotificationDefinitionRecordConsts.MaxPermissionNameLength);
            b.Property(x => x.FeatureName).HasMaxLength(NotificationDefinitionRecordConsts.MaxFeatureNameLength);

            b.HasIndex(x => new { x.Name }).IsUnique();
            b.HasIndex(x => new { x.GroupName });

            b.ApplyObjectExtensionMappings();
        });

        builder.TryConfigureObjectExtensions<NotificationDefinitionStoreDbContext>();
    }
}
