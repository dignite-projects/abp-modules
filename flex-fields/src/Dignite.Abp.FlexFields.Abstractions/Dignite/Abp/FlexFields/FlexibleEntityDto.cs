using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Base DTO shape for exposing a host entity's flex field value bag across a service boundary.
/// Downstream composes its own Mapperly/AutoMapper mapping on top of this - the kernel has no
/// Application layer (flexfields-design.md §1), so it doesn't own that mapping itself.
/// </summary>
[Serializable]
public abstract class FlexibleEntityDto : IHasFlexFields
{
    public FlexFieldDictionary FlexFields { get; set; }

    protected FlexibleEntityDto()
    {
        FlexFields = new FlexFieldDictionary();
    }

    protected FlexibleEntityDto(FlexFieldDictionary flexFields)
    {
        FlexFields = flexFields;
    }
}
