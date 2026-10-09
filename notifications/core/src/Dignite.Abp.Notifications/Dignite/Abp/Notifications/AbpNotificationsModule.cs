using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Features;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications;

/// <summary>
/// What a business module needs to define and publish notifications: definitions, routing, and the contracts
/// (<see cref="INotificationPublisher"/>, <see cref="INotificationStore"/>, <see cref="INotificationDistributor"/>,
/// <see cref="INotificationPermissionChecker"/>). It implements none of the pipeline: the host adds
/// <c>Dignite.Abp.Notifications.Distribution</c> (it delivers notifications itself) or
/// <c>Dignite.Abp.Notifications.Remote</c> (another process does). With neither, <see cref="INotificationPublisher"/>
/// has no implementation and the first attempt to resolve it fails.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpFeaturesModule)
    )]
public class AbpNotificationsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AutoAddDefinitionProviders(context.Services);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
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
