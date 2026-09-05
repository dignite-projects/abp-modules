using System.Linq;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields;

public class FieldTypeResolver_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly IFieldTypeResolver _resolver;

    public FieldTypeResolver_Tests()
    {
        _resolver = GetRequiredService<IFieldTypeResolver>();
    }

    [Fact]
    public void Should_Select_Text_Field_Type()
    {
        _resolver.Get(TextFieldType.ControlName).ShouldBeAssignableTo<TextFieldType>();
    }

    [Fact]
    public void Should_Enumerate_Every_Registered_Field_Type()
    {
        var names = _resolver.GetAll().Select(fieldType => fieldType.Name).ToList();

        names.ShouldContain(TextFieldType.ControlName);
        // Every name Get() accepts has to come back out of GetAll(): a downstream builds its field type
        // picker from GetAll() and then stores whatever the admin picked, so a name missing here would be
        // a type nobody can choose.
        names.ShouldAllBe(name => _resolver.Get(name).Name == name);
    }

    [Fact]
    public void Should_Report_Every_Scalar_Built_In_As_Indexable_And_Every_Composite_One_As_Not()
    {
        // Being composite is exactly what makes a field type unindexable: its value is a list of
        // composite objects (blocks/rows), not a scalar or a list of scalars, so there is no typed index
        // column to decompose it into - see MatrixFieldType's own remarks. Asserting the two halves
        // against ICompositeFieldType rather than against a name list keeps this honest for a built-in
        // added later: a new scalar type arriving without an index slot, or a composite one arriving
        // with one, both fail here first, which is where that decision should be visible.
        foreach (var fieldType in _resolver.GetAll())
        {
            if (fieldType is ICompositeFieldType)
            {
                fieldType.IsIndexable().ShouldBeFalse($"{fieldType.Name} is composite but has an IndexValueType");
            }
            else
            {
                fieldType.IsIndexable().ShouldBeTrue($"{fieldType.Name} has no IndexValueType");
            }
        }
    }
}
