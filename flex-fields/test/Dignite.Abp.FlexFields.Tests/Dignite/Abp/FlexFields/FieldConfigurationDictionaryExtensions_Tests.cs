using Dignite.Abp.FlexFields.Numeric;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields;

public class FieldConfigurationDictionaryExtensions_Tests
{
    [Fact]
    public void Empty_strings_read_as_null_for_nullable_numeric_configuration()
    {
        var configuration = new NumericConfiguration(
            new FieldConfigurationDictionary
            {
                [NumericConfigurationNames.Max] = "",
                [NumericConfigurationNames.Min] = " ",
                [NumericConfigurationNames.Step] = "",
            });

        configuration.Max.ShouldBeNull();
        configuration.Min.ShouldBeNull();
        configuration.Step.ShouldBeNull();
    }

    [Fact]
    public void Empty_strings_read_as_the_configured_default_for_nullable_values()
    {
        var configuration = new FieldConfigurationDictionary
        {
            ["Size"] = "",
        };

        configuration.GetConfiguration<int?>("Size", 5).ShouldBe(5);
    }
}
