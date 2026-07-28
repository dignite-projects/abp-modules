using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Joins the two independent sibling layers - <see cref="FlexFieldsAbstractionsModule"/> (field
/// contracts and types) and <see cref="FlexFieldsDomainSharedModule"/> (column-length constants) - and
/// adds the DDD-aware seams a host implements or consumes: the provider seam, validation, and index
/// maintenance. Same shape as ABP's own <c>AbpUsersDomainModule</c>.
/// </summary>
[DependsOn(
    typeof(FlexFieldsAbstractionsModule),
    typeof(FlexFieldsDomainSharedModule),
    typeof(AbpDddDomainModule)
    )]
public class FlexFieldsDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Registered explicitly as open generics rather than by convention: the type parameters map
        // 1:1, so one registration covers every host entity type, and a host can Replace it.
        context.Services.AddTransient(typeof(IFlexFieldValidator<>), typeof(FlexFieldValidator<>));

        // Unlike IFlexFieldIndexManager, this needs no provider-specific implementation - moving a key
        // inside the value bag is the same operation whatever stores it.
        context.Services.AddTransient(typeof(IFlexFieldValueMigrator<>), typeof(FlexFieldValueMigrator<>));
    }
}
