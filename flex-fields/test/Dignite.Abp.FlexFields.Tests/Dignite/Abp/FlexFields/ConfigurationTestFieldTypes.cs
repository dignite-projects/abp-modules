using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FlexFields;

// The field types the configuration tests need: each is registered through DI like any downstream's, so
// the tests go through the resolver and the validator exactly as a host would. All of them are indexable,
// because FieldTypeResolver_Tests holds every non-composite registered type to that.

/// <summary>Reads its configuration but says nothing about it - the shape of every field type written before keys could be declared.</summary>
public class UndeclaredConfigurationFieldType : FieldTypeBase
{
    public const string ControlName = "Test.Undeclared";

    public override string Name => ControlName;

    public override string DisplayName => ControlName;

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args) => Array.Empty<ValidationResult>();

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration) => new OpaqueConfiguration(configuration);
}

/// <summary>Says it takes no configuration at all.</summary>
public class NoConfigurationFieldType : FieldTypeBase
{
    public const string ControlName = "Test.NoConfiguration";

    public override string Name => ControlName;

    public override string DisplayName => ControlName;

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Array.Empty<FieldConfigurationKey>();

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args) => Array.Empty<ValidationResult>();

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration) => new OpaqueConfiguration(configuration);
}

/// <summary>Cannot work without one key, as Site's file field cannot without a container.</summary>
public class RequiredKeyFieldType : FieldTypeBase
{
    public const string ControlName = "Test.RequiredKey";

    public const string ContainerKey = "Test.RequiredKey.Container";

    public override string Name => ControlName;

    public override string DisplayName => ControlName;

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys { get; } = new[]
    {
        new FieldConfigurationKey(ContainerKey, FieldConfigurationValueKind.String)
        {
            Required = true,
            Description = "The container."
        },
        new FieldConfigurationKey("Test.RequiredKey.Id", FieldConfigurationValueKind.Guid)
        {
            EmptyStringIsUnset = true,
            Description = "A GUID that may be cleared."
        }
    };

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args) => Array.Empty<ValidationResult>();

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration) => new OpaqueConfiguration(configuration);
}

/// <summary>
/// Implements <see cref="IFieldType"/> itself instead of deriving <see cref="FieldTypeBase"/>, the way a
/// downstream written against the interface alone would. It was compiled without knowing
/// <see cref="IFieldType.ConfigurationKeys"/> existed - as far as this class is concerned, it still does not.
/// </summary>
public class DirectInterfaceFieldType : IFieldType, ITransientDependency
{
    public const string ControlName = "Test.DirectInterface";

    public string Name => ControlName;

    public string DisplayName => ControlName;

    public FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration) => new OpaqueConfiguration(configuration);

    public IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args) => Array.Empty<ValidationResult>();

    public IEnumerable<object> GetSearchableValues(FlexFieldValue field) => Array.Empty<object>();
}

/// <summary>
/// A composite type that says nothing about its own keys, as a downstream written before keys could be declared
/// would be: its inline fields are still its configuration's to declare, and are still checked. Composite, so not
/// indexable - FieldTypeResolver_Tests holds it to that too.
/// </summary>
public class UndeclaredCompositeFieldType : FieldTypeBase, ICompositeFieldType
{
    public const string ControlName = "Test.UndeclaredComposite";

    public const string FieldsKey = "Test.UndeclaredComposite.Fields";

    public override string Name => ControlName;

    public override string DisplayName => ControlName;

    public override FlexFieldValueType? IndexValueType => null;

    public IEnumerable<InlineFieldDefinition> GetInlineFields(FieldConfigurationDictionary configuration)
        => configuration.GetConfiguration(FieldsKey, new List<InlineFieldDefinition>());

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args) => Array.Empty<ValidationResult>();

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration) => new OpaqueConfiguration(configuration);
}

public class OpaqueConfiguration : FieldConfigurationBase
{
    public OpaqueConfiguration(FieldConfigurationDictionary configuration)
        : base(configuration)
    {
    }
}
