using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Holds only the constants the domain and the persistence providers must agree on
/// (<see cref="FlexFieldConsts"/>) - deliberately dependency-free, like ABP's own
/// <c>AbpUsersDomainSharedModule</c>. Field contracts, configuration and localization live in
/// <c>Dignite.Abp.FlexFields.Abstractions</c>, which does NOT reference this project.
/// </summary>
public class FlexFieldsDomainSharedModule : AbpModule
{
}
