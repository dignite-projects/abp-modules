using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Asserts the shape the three kernel <c>EntityTypeBuilder</c> extensions produce, by inspecting the
/// built EF Core model directly. No ABP host needed - the model is the whole subject here.
/// </summary>
public class FlexFieldsModel_Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestFlexFieldsDbContext _dbContext;
    private readonly IModel _model;

    public FlexFieldsModel_Tests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _dbContext = new TestFlexFieldsDbContext(
            new DbContextOptionsBuilder<TestFlexFieldsDbContext>().UseSqlite(_connection).Options);
        _model = _dbContext.Model;
    }

    [Fact]
    public void Value_bag_column_is_named_after_the_contract_member()
    {
        Property<TestArticle>(nameof(IHasFlexFields.FlexFields))
            .GetColumnName().ShouldBe(nameof(IHasFlexFields.FlexFields));
    }

    [Fact]
    public void Field_definition_columns_are_named_after_the_contract_members()
    {
        foreach (var member in new[]
                 {
                     nameof(IFlexField.Name), nameof(IFlexField.DisplayName), nameof(IFlexField.Description),
                     nameof(IFlexField.FieldTypeName), nameof(IFlexField.Configuration)
                 })
        {
            Property<TestField>(member).GetColumnName().ShouldBe(member);
        }
    }

    [Fact]
    public void Index_row_columns_are_named_after_the_contract_members()
    {
        foreach (var member in new[]
                 {
                     nameof(IFlexFieldIndex.FieldId), nameof(IFlexFieldIndex.ValueType),
                     nameof(IFlexFieldIndex.StringValue), nameof(IFlexFieldIndex.NumberValue),
                     nameof(IFlexFieldIndex.DateTimeValue), nameof(IFlexFieldIndex.BooleanValue),
                     nameof(IFlexFieldIndex.GuidValue)
                 })
        {
            Property<TestArticleFlexFieldIndex>(member).GetColumnName().ShouldBe(member);
        }
    }

    [Fact]
    public void Max_lengths_come_from_FlexFieldConsts()
    {
        Property<TestField>(nameof(IFlexField.Name)).GetMaxLength().ShouldBe(FlexFieldConsts.MaxNameLength);
        Property<TestField>(nameof(IFlexField.DisplayName)).GetMaxLength().ShouldBe(FlexFieldConsts.MaxDisplayNameLength);
        Property<TestField>(nameof(IFlexField.Description)).GetMaxLength().ShouldBe(FlexFieldConsts.MaxDescriptionLength);
        Property<TestField>(nameof(IFlexField.FieldTypeName)).GetMaxLength().ShouldBe(FlexFieldConsts.MaxFieldTypeNameLength);
        Property<TestArticleFlexFieldIndex>(nameof(IFlexFieldIndex.StringValue)).GetMaxLength()
            .ShouldBe(FlexFieldConsts.MaxStringValueLength);
    }

    [Fact]
    public void Required_columns_are_required_and_optional_ones_are_not()
    {
        Property<TestField>(nameof(IFlexField.Name)).IsNullable.ShouldBeFalse();
        Property<TestField>(nameof(IFlexField.DisplayName)).IsNullable.ShouldBeFalse();
        Property<TestField>(nameof(IFlexField.FieldTypeName)).IsNullable.ShouldBeFalse();
        Property<TestField>(nameof(IFlexField.Description)).IsNullable.ShouldBeTrue();

        Property<TestArticleFlexFieldIndex>(nameof(IFlexFieldIndex.FieldId)).IsNullable.ShouldBeFalse();
        foreach (var slot in new[]
                 {
                     nameof(IFlexFieldIndex.StringValue), nameof(IFlexFieldIndex.NumberValue),
                     nameof(IFlexFieldIndex.DateTimeValue), nameof(IFlexFieldIndex.BooleanValue),
                     nameof(IFlexFieldIndex.GuidValue)
                 })
        {
            Property<TestArticleFlexFieldIndex>(slot).IsNullable.ShouldBeTrue();
        }
    }

    [Fact]
    public void Json_columns_carry_a_value_comparer_so_in_place_mutation_is_tracked()
    {
        // Without these, EF Core compares the converted dictionary by reference and silently drops
        // in-place edits. Asserted here because the symptom (a lost UPDATE) is easy to miss.
        Property<TestArticle>(nameof(IHasFlexFields.FlexFields))
            .GetValueComparer().ShouldBeOfType<ValueComparers.FlexFieldDictionaryValueComparer>();
        Property<TestField>(nameof(IFlexField.Configuration))
            .GetValueComparer().ShouldBeOfType<ValueComparers.FieldConfigurationDictionaryValueComparer>();
    }

    [Fact]
    public void Kernel_contributes_no_table_of_its_own()
    {
        // Every mapped table is one this downstream named; the kernel only ever added columns.
        _model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(name => name != null)
            .Distinct()
            .ShouldBe(new[] { "TestArticles", "TestFields", "TestArticleFlexFieldIndexes" }, ignoreOrder: true);
    }

    [Fact]
    public void Index_table_carries_the_downstreams_own_foreign_key_not_a_kernel_one()
    {
        var indexEntity = _model.FindEntityType(typeof(TestArticleFlexFieldIndex))!;

        var foreignKey = indexEntity.GetForeignKeys().ShouldHaveSingleItem();
        foreignKey.Properties.ShouldHaveSingleItem().Name.ShouldBe(nameof(TestArticleFlexFieldIndex.ArticleId));
        foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(TestArticle));

        // TEntity is a type marker only - it must not have produced a navigation or a shadow FK.
        indexEntity.GetNavigations().ShouldBeEmpty();
    }

    private IProperty Property<TEntity>(string name)
    {
        return _model.FindEntityType(typeof(TEntity))!.FindProperty(name)
               ?? throw new ShouldAssertException($"{typeof(TEntity).Name} has no mapped property '{name}'.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
