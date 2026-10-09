using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Features;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The contracts of Dignite.Abp.Notifications, after ABP's <c>AbpAuthorizationAbstractionsModule</c>: what a business
/// module needs to define and publish notifications (definitions, routing, <see cref="INotificationPublisher"/>), what
/// a notifier needs to deliver them (the payload types, <see cref="NotificationDeliveryRequestedEto"/>,
/// <see cref="INotificationNotifier"/>), and the contracts other packages implement (<see cref="INotificationStore"/>,
/// <see cref="INotificationDistributor"/>, <see cref="INotificationPermissionChecker"/>). It implements none of the
/// pipeline.
/// </summary>
[DependsOn(
    typeof(AbpLocalizationModule),
    typeof(AbpJsonSystemTextJsonModule),
    typeof(AbpMultiTenancyAbstractionsModule),
    typeof(AbpFeaturesModule)
    )]
public class AbpNotificationsAbstractionsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AutoAddDefinitionProviders(context.Services);
    }

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

        // Same fail-fast materialization for channel → notifier registrations: two notifier types claiming one
        // channel fail the application start rather than the first delivery on that channel.
        context.Services
            .AddOptions<NotificationNotifierOptions>()
            .Validate(_ => true)
            .ValidateOnStart();

        // Registers the polymorphic NotificationData converter on ABP's IJsonSerializer options for every
        // app-level JSON boundary (e.g. HttpApi.Client proxies reading UserNotificationDto.Data). It does NOT
        // cover the distributed event bus: ABP serializes ETOs with plain System.Text.Json, which is why
        // NotificationDeliveryRequestedEto and NotificationPublishRequestedEto carry pre-serialized DataJson instead of
        // a live NotificationData.
        context.Services
            .AddOptions<AbpSystemTextJsonSerializerOptions>()
            .Configure<INotificationDataTypeRegistry>((options, registry) =>
            {
                options.JsonSerializerOptions.Converters.Add(new NotificationDataJsonConverter(registry));
            });

        context.Services
            .AddOptions<NotificationRoutingOptions>()
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();

        // NotificationDefinitionRegistration.Validate() runs from NotificationDefinitionStartupService instead of this
        // options-validation pipeline: the real definition-name conflict check only exists inside
        // StaticNotificationDefinitionStore's lazily-built dictionary, so both checks belong at the one hook that can
        // reach it.
        context.Services.AddHostedService<NotificationDefinitionStartupService>();
    }

    private static void AutoAddDefinitionProviders(IServiceCollection services)
    {
        services.PostConfigure<NotificationDefinitionRegistration>(options =>
        {
            var definitionProviders = services
                .Where(descriptor => descriptor.ImplementationType != null &&
                                     typeof(INotificationDefinitionProvider).IsAssignableFrom(
                                         descriptor.ImplementationType))
                .Select(descriptor => descriptor.ImplementationType!)
                .Distinct()
                .ToList();

            options.DefinitionProviders.AddIfNotContains(definitionProviders);
        });
    }
}
