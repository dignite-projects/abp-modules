using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Runs each of a host entity's flex fields through its field type's own validation rules and collects
/// the results. The write-path counterpart of <see cref="IFlexFieldIndexManager{TEntity}"/>: both walk an
/// entity's fields via <see cref="IFlexFieldProvider{TEntity}"/> and dispatch to
/// <see cref="IFieldType"/> - one calls <see cref="IFieldType.Validate"/>, the other
/// <see cref="IFieldType.GetSearchableValues"/>.
/// <para>
/// Returns the errors rather than throwing, so a host can merge them with its own validation and choose
/// how to surface them (an <c>IValidatableObject</c> implementation, an explicit business exception, a
/// draft-save that tolerates them, ...).
/// </para>
/// </summary>
/// <typeparam name="TEntity">The host entity type, e.g. a CMS's <c>Entry</c>.</typeparam>
public interface IFlexFieldValidator<in TEntity>
    where TEntity : IHasFlexFields
{
    Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);
}
