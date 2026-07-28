using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Depends on <see cref="FlexFieldsAbstractionsModule"/> only - not the Domain module - so these tests
/// prove the layering claim: referencing Abstractions alone is enough to get the field types, the
/// resolver and localization.
/// </summary>
[DependsOn(
    typeof(AbpTestBaseModule),
    typeof(AbpAutofacModule),
    typeof(FlexFieldsAbstractionsModule)
    )]
public class DigniteAbpFlexFieldsTestModule : AbpModule
{
}
