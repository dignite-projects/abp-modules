using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Not indexable (<see cref="IndexValueType"/> is null), and deliberately does NOT self-guard in
/// <see cref="GetSearchableValues"/> the way <see cref="FieldTypeBase"/>'s own default implementation
/// does - standing in for a custom <see cref="IFieldType"/> that doesn't bother checking its own
/// <see cref="IndexValueType"/> before yielding values. Exists to prove
/// <c>EfCoreFlexFieldIndexManagerBase.ProjectAsync</c>'s own <c>IndexValueType == null</c> guard is
/// load-bearing rather than redundant: none of the built-in field types can exercise that guard, since
/// all of them report a non-null <see cref="IndexValueType"/>.
/// </summary>
public class NonIndexableFieldType : FieldTypeBase
{
    public const string ControlName = "NonIndexable";

    public override string Name => ControlName;

    public override string DisplayName => "Non-indexable (test only)";

    public override FlexFieldValueType? IndexValueType => null;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        return Array.Empty<ValidationResult>();
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new TestFieldConfiguration(fieldConfiguration);
    }

    public override IEnumerable<object> GetSearchableValues(FlexFieldValue field)
    {
        if (field.Value != null)
        {
            yield return field.Value;
        }
    }

    private class TestFieldConfiguration : FieldConfigurationBase
    {
        public TestFieldConfiguration(FieldConfigurationDictionary dictionary)
            : base(dictionary)
        {
        }
    }
}
