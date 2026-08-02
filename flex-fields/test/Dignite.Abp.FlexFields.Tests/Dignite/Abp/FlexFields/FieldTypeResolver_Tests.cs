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
    public void Should_Report_Every_Built_In_Field_Type_As_Indexable()
    {
        // All six built-ins have an IndexValueType. This is the counterpart of the Angular library's
        // "none of the built-ins mirror a null server IndexValueType" assertion - a new built-in arriving
        // without an index slot should be a deliberate decision, visible as a failure here first.
        foreach (var fieldType in _resolver.GetAll())
        {
            fieldType.IsIndexable().ShouldBeTrue($"{fieldType.Name} has no IndexValueType");
        }
    }
}
