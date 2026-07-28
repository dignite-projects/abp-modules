namespace Dignite.Abp.FlexFields;

/// <summary>
/// Marks a host entity as carrying a flex field value bag - the authoritative storage in the Y design
/// (flexfields-design.md §4). Deliberately self-built, not built on ABP's <c>IHasExtraProperties</c>.
/// </summary>
public interface IHasFlexFields
{
    FlexFieldDictionary FlexFields { get; }
}
