using Dignite.Abp.FlexFields.EntityFrameworkCore.ValueComparers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.ValueConverters;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Support, not ownership: every method here extends an <see cref="EntityTypeBuilder{TEntity}"/> the
/// <i>downstream</i> already opened for its <i>own</i> entity, and configures only the columns the
/// kernel's contracts define. Modeled on ABP's <c>AbpUsersDbContextModelCreatingExtensions</c>.
/// <para>
/// Consequently the kernel never calls <c>builder.Entity&lt;&gt;()</c>, <c>ToTable</c>,
/// <c>ConfigureByConvention</c>, <c>HasKey</c>, <c>HasIndex</c> or any relationship API, owns no
/// <c>DbContext</c> / <c>DbSet</c> / table-prefix, and cannot force an entity into anyone's model. Table
/// names, keys, indexes, foreign keys and migrations are all the downstream's.
/// </para>
/// <para>
/// Every property is pinned with <c>HasColumnName(nameof(...))</c> so the physical column names are
/// identical across all downstreams regardless of how each named its CLR properties.
/// </para>
/// </summary>
public static class FlexFieldsDbContextModelCreatingExtensions
{
    /// <summary>
    /// Maps a host entity's flex field value bag - the authoritative storage - to a single JSON column.
    /// Call from the downstream's own configuration of its host entity (e.g. a CMS's <c>Entry</c>).
    /// </summary>
    public static void ConfigureFlexFieldsProperty<TEntity>(this EntityTypeBuilder<TEntity> b)
        where TEntity : class, IHasFlexFields
    {
        b.Property(e => e.FlexFields)
            .HasConversion(new AbpJsonValueConverter<FlexFieldDictionary>())
            .HasColumnName(nameof(IHasFlexFields.FlexFields))
            // Required: a value-converted mutable dictionary is compared by reference otherwise, so
            // in-place SetField/RemoveField on a loaded entity would never be persisted.
            .Metadata.SetValueComparer(new FlexFieldDictionaryValueComparer());
    }

    /// <summary>
    /// Configures the columns of a flex field <b>definition</b>. Call from the downstream's own
    /// configuration of the entity that implements <see cref="IFlexField"/> (e.g. a CMS's <c>Field</c>).
    /// </summary>
    public static void ConfigureFlexField<TField>(this EntityTypeBuilder<TField> b)
        where TField : class, IFlexField
    {
        b.Property(f => f.Name).IsRequired().HasMaxLength(FlexFieldConsts.MaxNameLength)
            .HasColumnName(nameof(IFlexField.Name));
        b.Property(f => f.DisplayName).IsRequired().HasMaxLength(FlexFieldConsts.MaxDisplayNameLength)
            .HasColumnName(nameof(IFlexField.DisplayName));
        b.Property(f => f.Description).HasMaxLength(FlexFieldConsts.MaxDescriptionLength)
            .HasColumnName(nameof(IFlexField.Description));
        b.Property(f => f.FieldTypeName).IsRequired().HasMaxLength(FlexFieldConsts.MaxFieldTypeNameLength)
            .HasColumnName(nameof(IFlexField.FieldTypeName));
        b.Property(f => f.Configuration)
            .HasConversion(new AbpJsonValueConverter<FieldConfigurationDictionary>())
            .HasColumnName(nameof(IFlexField.Configuration))
            .Metadata.SetValueComparer(new FieldConfigurationDictionaryValueComparer());
    }

    /// <summary>
    /// Configures the typed value columns of a query-index row. Call from the downstream's own
    /// configuration of its <see cref="FlexFieldIndexBase{TEntity}"/> subclass - which is also where the
    /// table name, the foreign key to the host, and any covering indexes belong.
    /// </summary>
    public static void ConfigureFlexFieldIndex<TIndex>(this EntityTypeBuilder<TIndex> b)
        where TIndex : class, IFlexFieldIndex
    {
        b.Property(x => x.FieldId).IsRequired().HasColumnName(nameof(IFlexFieldIndex.FieldId));
        b.Property(x => x.ValueType).IsRequired().HasColumnName(nameof(IFlexFieldIndex.ValueType));
        b.Property(x => x.StringValue).HasMaxLength(FlexFieldConsts.MaxStringValueLength)
            .HasColumnName(nameof(IFlexFieldIndex.StringValue));
        b.Property(x => x.NumberValue).HasColumnName(nameof(IFlexFieldIndex.NumberValue));
        b.Property(x => x.DateTimeValue).HasColumnName(nameof(IFlexFieldIndex.DateTimeValue));
        b.Property(x => x.BooleanValue).HasColumnName(nameof(IFlexFieldIndex.BooleanValue));
        b.Property(x => x.GuidValue).HasColumnName(nameof(IFlexFieldIndex.GuidValue));
    }
}
