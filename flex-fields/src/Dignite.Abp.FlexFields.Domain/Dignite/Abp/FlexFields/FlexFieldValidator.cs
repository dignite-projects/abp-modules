using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Provider-agnostic default <see cref="IFlexFieldValidator{TEntity}"/>: resolves the entity's fields
/// through the host's <see cref="IFlexFieldProvider{TEntity}"/> and asks each field's own
/// <see cref="IFieldType"/> to validate it.
/// <para>
/// Registered as an open generic by <see cref="FlexFieldsDomainModule"/>, so it serves every host entity
/// type without per-type registration. Members are virtual, and a host that needs different behaviour
/// (cross-field rules, short-circuiting on the first error) can subclass or replace it.
/// </para>
/// </summary>
public class FlexFieldValidator<TEntity> : IFlexFieldValidator<TEntity>
    where TEntity : IHasFlexFields
{
    protected IFlexFieldProvider<TEntity> FlexFieldProvider { get; }

    protected IFieldTypeResolver FieldTypeResolver { get; }

    public FlexFieldValidator(
        IFlexFieldProvider<TEntity> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver)
    {
        FlexFieldProvider = flexFieldProvider;
        FieldTypeResolver = fieldTypeResolver;
    }

    /// <summary>
    /// Every field is validated, not just up to the first failure - a form should report all of its
    /// problems at once rather than one per round trip.
    /// </summary>
    public virtual async Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationResult>();

        foreach (var field in await FlexFieldProvider.GetFlexFieldsAsync(entity, cancellationToken))
        {
            var fieldType = FieldTypeResolver.Get(field.FieldTypeName);
            errors.AddRange(fieldType.Validate(new FieldValidationArgs(field)));
        }

        return errors;
    }
}
