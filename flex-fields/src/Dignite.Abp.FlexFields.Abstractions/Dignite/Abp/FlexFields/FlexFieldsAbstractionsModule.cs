using Dignite.Abp.FlexFields.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Everything a downstream host needs to describe and handle flex fields: the
/// <see cref="IFlexFieldData"/>/<see cref="FlexFieldData"/> plain-data contract, the
/// <see cref="FlexFieldValue"/> runtime carrier, the value bag (<see cref="IHasFlexFields"/>), and the
/// built-in field types. Referencing this package alone is enough to implement a custom
/// <see cref="IFieldType"/>, and it is also what a downstream's Application.Contracts references for the
/// DTO-facing types.
/// <para>
/// Self-contained on purpose: it references no other package of this module, so a downstream taking it
/// takes exactly one dependency. It also cannot see <c>Volo.Abp.Ddd.Domain</c> and defines no Entity
/// contract - persistence and DDD start at <c>Dignite.Abp.FlexFields.Domain</c>, the same layering ABP's
/// own <c>Volo.Abp.Users.Abstractions</c> uses for <c>IUserData</c> versus <c>IUser</c>.
/// </para>
/// </summary>
[DependsOn(
    typeof(AbpLocalizationModule)
    )]
public class FlexFieldsAbstractionsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FlexFieldsAbstractionsModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FlexFieldsResource>("en")
                .AddVirtualJson("/Dignite/Abp/FlexFields/Localization/Resources");
        });
    }
}
