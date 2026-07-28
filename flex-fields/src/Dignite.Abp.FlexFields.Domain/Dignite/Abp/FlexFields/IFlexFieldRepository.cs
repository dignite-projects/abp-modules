using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Repository for a downstream's own <see cref="IFlexField"/> entity (e.g. a CMS's <c>Field</c>) - a
/// convenience for the downstream, the same shape as ABP's <c>IUserRepository&lt;TUser&gt;</c>.
/// <para>
/// This walks the <b>field-definition axis</b> and the kernel itself never calls it - contrast
/// <see cref="IFlexFieldProvider{TEntity}"/>, which walks the <b>host-entity axis</b> and is the kernel's
/// only way to learn what fields a host entity has. The two are not interchangeable: resolving a host
/// entity's fields needs per-usage Required/Searchable flags and the entity's own value bag, neither of
/// which a field repository has access to.
/// </para>
/// <para>
/// The EF Core implementation is <c>EfCoreFlexFieldRepositoryBase&lt;TDbContext, TField&gt;</c>
/// (<c>Dignite.Abp.FlexFields.EntityFrameworkCore</c>) - see its remarks before assuming this interface
/// resolves from DI for free; a downstream's concrete subclass usually needs one explicit
/// <c>AddTransient</c> line for it, on top of the constructor.
/// </para>
/// </summary>
/// <typeparam name="TField">The downstream's own field-definition entity type.</typeparam>
public interface IFlexFieldRepository<TField> : IBasicRepository<TField, Guid>
    where TField : class, IFlexField
{
    /// <summary>
    /// Finds the field definition by its unique <see cref="IFlexField.Name"/> - the key a host's value bag
    /// is indexed by.
    /// </summary>
    Task<TField?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-loads field definitions by id - the direction a query index row (keyed by field id) needs to
    /// resolve back to its definition.
    /// </summary>
    Task<List<TField>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether another field already has <paramref name="name"/> - <see cref="IFlexField.Name"/> is
    /// documented as unique. Pass <paramref name="excludedId"/> when checking a rename.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default);
}
