using System.Collections.Generic;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Table;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="CompositeFieldNesting.ExceedsMaxDepth"/> against real registered field types - the rule a
/// host applies when it accepts a field definition, and the only thing standing between a client-supplied
/// configuration and every recursive reader of it (validation, the Razor views, the Angular editors).
/// </summary>
public class CompositeFieldNesting_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly IReadOnlyList<IFieldType> _fieldTypes;

    public CompositeFieldNesting_Tests()
    {
        _fieldTypes = GetRequiredService<IFieldTypeResolver>().GetAll();
    }

    [Fact]
    public void A_Scalar_Field_Never_Exceeds_The_Limit()
    {
        CompositeFieldNesting
            .ExceedsMaxDepth(TextFieldType.ControlName, new FieldConfigurationDictionary(), _fieldTypes)
            .ShouldBeFalse();
    }

    [Fact]
    public void A_Table_Of_Scalar_Columns_Is_Within_The_Limit()
    {
        CompositeFieldNesting
            .ExceedsMaxDepth(TableFieldType.ControlName, ScalarColumnTable(), _fieldTypes)
            .ShouldBeFalse();
    }

    /// <summary>
    /// A composite that declares nothing yet is one level, not "unbounded" - refusing it would block an
    /// admin's very first save of a new Table field, before any column exists.
    /// </summary>
    [Fact]
    public void A_Composite_With_No_Inline_Fields_Is_Within_The_Limit()
    {
        CompositeFieldNesting
            .ExceedsMaxDepth(TableFieldType.ControlName, new FieldConfigurationDictionary(), _fieldTypes)
            .ShouldBeFalse();
    }

    /// <summary>
    /// <c>Table &gt; Matrix &gt; Table</c>, the innermost one holding an ordinary Text column: four levels
    /// of field definition, one past <see cref="CompositeFieldNesting.MaxDepth"/>.
    /// </summary>
    [Fact]
    public void A_Table_Nesting_A_Matrix_Nesting_A_Table_Exceeds_The_Limit()
    {
        var matrixConfiguration = new FieldConfigurationDictionary();
        _ = new MatrixConfiguration(matrixConfiguration)
        {
            BlockTypes = new List<MatrixBlockType>
            {
                new()
                {
                    Name = "grid",
                    DisplayName = "Grid",
                    Fields = new List<InlineFieldDefinition>
                    {
                        new()
                        {
                            Name = "inner",
                            DisplayName = "Inner",
                            FieldTypeName = TableFieldType.ControlName,
                            Configuration = ScalarColumnTable()
                        }
                    }
                }
            }
        };

        var outerConfiguration = new FieldConfigurationDictionary();
        _ = new TableConfiguration(outerConfiguration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new()
                {
                    Name = "blocks",
                    DisplayName = "Blocks",
                    FieldTypeName = MatrixFieldType.ControlName,
                    Configuration = matrixConfiguration
                }
            }
        };

        CompositeFieldNesting
            .ExceedsMaxDepth(TableFieldType.ControlName, outerConfiguration, _fieldTypes)
            .ShouldBeTrue();
    }

    /// <summary>
    /// An unregistered type name is not this measurement's error to raise - nothing can be nested under a
    /// type nobody knows, so it reads as a leaf. <c>InlineFieldValidator</c> reports it when a value is
    /// validated instead.
    /// </summary>
    [Fact]
    public void An_Unregistered_Field_Type_Name_Is_Treated_As_A_Leaf()
    {
        CompositeFieldNesting
            .ExceedsMaxDepth("NoSuchFieldType", ScalarColumnTable(), _fieldTypes)
            .ShouldBeFalse();
    }

    /// <summary>
    /// The reason <c>MeasureDepth</c> carries a remaining-budget parameter: this is the first thing to walk
    /// a configuration that arrived from a client and has not been vetted, so it has to stop on its own.
    /// A ten-deep chain answers in bounded work rather than recursing to the bottom - if the budget were
    /// removed, this test would not fail, it would take the test host down with a stack overflow.
    /// </summary>
    [Fact]
    public void A_Deeply_Nested_Configuration_Terminates_And_Is_Refused()
    {
        CompositeFieldNesting
            .ExceedsMaxDepth(TableFieldType.ControlName, NestedTables(10), _fieldTypes)
            .ShouldBeTrue();
    }

    /// <summary>One Text column - the leaf of every nesting chain below.</summary>
    private static FieldConfigurationDictionary ScalarColumnTable()
    {
        var configuration = new FieldConfigurationDictionary();

        _ = new TableConfiguration(configuration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new() { Name = "label", DisplayName = "Label", FieldTypeName = TextFieldType.ControlName }
            }
        };

        return configuration;
    }

    /// <summary>
    /// A Table whose single column is a Table whose single column is... <paramref name="levels"/> deep,
    /// bottoming out in <see cref="ScalarColumnTable"/>.
    /// </summary>
    private static FieldConfigurationDictionary NestedTables(int levels)
    {
        if (levels <= 1)
        {
            return ScalarColumnTable();
        }

        var configuration = new FieldConfigurationDictionary();

        _ = new TableConfiguration(configuration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new()
                {
                    Name = "nested",
                    DisplayName = "Nested",
                    FieldTypeName = TableFieldType.ControlName,
                    Configuration = NestedTables(levels - 1)
                }
            }
        };

        return configuration;
    }
}
