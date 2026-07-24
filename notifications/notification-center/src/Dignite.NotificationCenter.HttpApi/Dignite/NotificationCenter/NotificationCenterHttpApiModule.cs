using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
    )]
public class NotificationCenterHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // Make MVC discover the controllers defined in this HttpApi assembly (UserNotificationController +
        // NotificationSubscriptionController). These are explicit controllers that delegate to
        // IUserNotificationAppService / INotificationSubscriptionAppService — not conventional/auto API controllers,
        // so this application-part registration (not ConventionalControllers.Create) is what's needed.
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(NotificationCenterHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // AbpNotificationsAbstractionsModule registers NotificationDataJsonConverter on AbpSystemTextJsonSerializerOptions
        // (what the distributed event bus / INotificationDataSerializer use), but ASP.NET Core's actual HTTP
        // response JSON formatter reads Microsoft.AspNetCore.Mvc.JsonOptions instead — a separate, unsynced
        // JsonSerializerOptions. Without this, UserNotificationDto.Data serializes over HTTP via System.Text.Json's
        // default reflection-based output for its declared type (NotificationData), silently dropping the
        // discriminator and every derived-type field — exactly what notifications-invariants.md §1 requires the
        // HTTP API to avoid.
        context.Services
            .AddOptions<JsonOptions>()
            .Configure<INotificationDataTypeRegistry>((options, registry) =>
            {
                options.JsonSerializerOptions.Converters.Add(new NotificationDataJsonConverter(registry));
            });
    }
}
