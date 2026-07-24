using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications;

[DependsOn(
    typeof(AbpLocalizationModule),
    typeof(AbpJsonSystemTextJsonModule)
    )]
public class AbpNotificationsAbstractionsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Force options materialization at startup so duplicate/ambiguous discriminator registrations fail fast
        // (the discriminator dictionary rejects conflicts as they are added).
        context.Services
            .AddOptions<NotificationDataOptions>()
            .Validate(_ => true)
            .ValidateOnStart();

        Configure<NotificationDataOptions>(options =>
        {
            options.Add<MessageNotificationData>();
            options.Add<LocalizableMessageNotificationData>();
            options.Add<UnsupportedNotificationData>();
        });

        // Registers the polymorphic NotificationData converter on ABP's IJsonSerializer options for every
        // app-level JSON boundary (e.g. HttpApi.Client proxies reading UserNotificationDto.Data). It does NOT
        // cover the distributed event bus: ABP serializes ETOs with plain System.Text.Json, which is why
        // NotificationDeliveryRequestedEto carries pre-serialized DataJson instead of a live NotificationData.
        context.Services
            .AddOptions<AbpSystemTextJsonSerializerOptions>()
            .Configure<INotificationDataTypeRegistry>((options, registry) =>
            {
                options.JsonSerializerOptions.Converters.Add(new NotificationDataJsonConverter(registry));
            });
    }
}
