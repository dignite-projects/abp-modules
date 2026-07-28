namespace Dignite.Abp.FlexFields;

/// <summary>
/// Bridges a downstream's <see cref="IFlexField"/> entity to the DDD-free <see cref="FlexFieldData"/> the
/// kernel's <see cref="FlexFieldValue"/> carries - the same one-way bridge ABP's own
/// <c>AbpUserExtensions.ToAbpUserData()</c> is for <c>IUser</c> -&gt; <c>IUserData</c>. Domain knows
/// Abstractions, never the reverse, so there is no <c>ToFlexField()</c> the other way; a host's
/// <c>IFlexFieldProvider&lt;TEntity&gt;</c> implementation calls this when it resolves an entity's fields.
/// </summary>
public static class FlexFieldExtensions
{
    public static FlexFieldData ToFlexFieldData(this IFlexField field)
    {
        return new FlexFieldData(
            id: field.Id,
            name: field.Name,
            displayName: field.DisplayName,
            fieldTypeName: field.FieldTypeName,
            description: field.Description,
            configuration: field.Configuration);
    }
}
