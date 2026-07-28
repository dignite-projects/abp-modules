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
}
