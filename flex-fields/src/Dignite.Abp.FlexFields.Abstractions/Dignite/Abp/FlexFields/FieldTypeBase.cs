using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Abp.FlexFields.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Base class for field types.
/// </summary>
public abstract class FieldTypeBase : IFieldType, ITransientDependency
{
    public IAbpLazyServiceProvider LazyServiceProvider { get; set; } = default!;

    protected IStringLocalizerFactory StringLocalizerFactory => LazyServiceProvider.LazyGetRequiredService<IStringLocalizerFactory>();

    protected IStringLocalizer L {
        get {
            if (_localizer == null)
            {
                _localizer = CreateLocalizer();
            }

            return _localizer;
        }
    }

    private IStringLocalizer? _localizer;

    protected Type LocalizationResource {
        get => _localizationResource;
        set {
            _localizationResource = value;
            _localizer = null;
        }
    }

    private Type _localizationResource = typeof(FlexFieldsResource);

    public abstract string Name { get; }

    public abstract string DisplayName { get; }

    public abstract FlexFieldValueType? IndexValueType { get; }

    public abstract IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args);

    public abstract FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration);

    /// <summary>
    /// Default single-value decomposition: the raw value itself, when the field type is indexable, the
    /// usage is searchable, and there is a value. Multi-valued field types (Select, Tree) override this to
    /// yield one per selected value.
    /// </summary>
    public virtual IEnumerable<object> GetSearchableValues(FlexFieldValue field)
    {
        if (IndexValueType == null || !field.Searchable || field.Value == null)
        {
            yield break;
        }

        yield return field.Value;
    }

    protected virtual IStringLocalizer CreateLocalizer()
    {
        return StringLocalizerFactory.Create(LocalizationResource);
    }
}
