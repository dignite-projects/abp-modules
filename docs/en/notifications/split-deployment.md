```json
//[doc-seo]
{
    "Description": "Run the Dignite Notifications module as a separate notification service: publishers send notifications through the outbox, and share their definitions through the definition store."
}
```

# Split Deployment

A service does not have to host the inbox and the channels to publish notifications. In a [microservice](https://abp.io/docs/latest/solution-templates/microservice) solution, the services that only publish install `Dignite.Abp.Notifications.Client`, and one **notification service** owns the inbox, the subscriptions, the push devices and the channels. The business modules are the same in both topologies: they reference `Dignite.Abp.Notifications.Abstractions` and call `INotificationPublisher`.

````text
Publisher service
  business module ──► INotificationPublisher (remote)
                        │ definition checked · channels resolved · payload serialized
                        ▼
                      NotificationPublishRequestedEto ──► outbox ──► message broker
  at startup: saves its notification definitions ──► definition store (shared tables)

Notification service
  event inbox ──► publish request handler ──► distributor (inline, or its own background job)
                    │ definition known? otherwise throws, and the inbox retries
                    │ recipients · eligibility · inbox rows
                    ▼
                  NotificationDeliveryRequestedEto ──► SignalR / Email / Push
````

## The Publisher Service

Install `Dignite.Abp.Notifications.Client`, and the definition store for your database provider, `Dignite.Abp.Notifications.EntityFrameworkCore` or `Dignite.Abp.Notifications.MongoDB`. No channel, no Notification Center and no `Dignite.Abp.Notifications`:

````csharp
[DependsOn(
    typeof(MyBusinessApplicationModule), // defines and publishes notifications
    typeof(AbpNotificationsClientModule),
    typeof(AbpNotificationsEntityFrameworkCoreModule) // or AbpNotificationsMongoDbModule
)]
public class MyServiceModule : AbpModule
{
}
````

Map the publisher's `NotificationCenter` [connection string](https://abp.io/docs/latest/framework/fundamentals/connection-strings) to the notification service's database, where the definition store's tables live.

The remote publisher does only what needs the publisher's process, and then sends exactly **one** `NotificationPublishRequestedEto` per notification, whatever the number of recipients:

1. The definition must exist in the publisher; publishing an undefined name throws while the caller is still waiting, as with the local publisher.
2. The payload is serialized once, with its type name.
3. The channels are resolved here, with the publisher's [routes](routing.md): the routes are configured where the notifications are defined.
4. The event is published. Inside a unit of work it goes to the publisher's [outbox](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed), in the same transaction as the business change, when the outbox is enabled on the publisher's database.

## The Notification Service

Install the Notification Center, which brings `Dignite.Abp.Notifications`, its channels and the definition store, exactly like a monolith with an inbox, and turn the dynamic stores on:

````csharp
[DependsOn(
    typeof(AbpNotificationsSignalRModule),
    typeof(AbpNotificationCenterApplicationModule),
    typeof(AbpNotificationCenterHttpApiModule),
    typeof(AbpNotificationCenterEntityFrameworkCoreModule), // or AbpNotificationCenterMongoDbModule
    typeof(AbpNotificationsEntityFrameworkCoreModule)       // or AbpNotificationsMongoDbModule
)]
public class NotificationServiceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Read the definitions the publishers saved
        Configure<NotificationDefinitionStoreOptions>(options =>
        {
            options.IsDynamicNotificationStoreEnabled = true;
        });

        // Know the permissions and features other services define
        Configure<PermissionManagementOptions>(options =>
        {
            options.IsDynamicPermissionStoreEnabled = true;
        });
        Configure<FeatureManagementOptions>(options =>
        {
            options.IsDynamicFeatureStoreEnabled = true;
        });

        // Retry a publish request for a definition not read yet, with back-off
        Configure<AbpEventBusBoxesOptions>(options =>
        {
            options.InboxProcessorFailurePolicy = InboxProcessorFailurePolicy.RetryLater;
        });
    }
}
````

Also configure ABP's **event inbox** on the service. It deduplicates the publish requests by message id, and runs the handler, the inbox rows and the outbox records of the deliveries in one transaction; see [Delivery Guarantees](delivery-guarantees.md).

The service turns each publish request into a notification with the publisher's notification id, tenant, payload and channels, and distributes it as the local publisher would: a small explicit fan-out inline, anything else with its own [background job](publishing-notifications.md#inline-or-in-the-background). The eligibility checks, the inbox rows and the deliveries all happen there, in the notification's tenant. The channels the publisher resolved are used as they are; the service's own routes are not consulted for them.

* **Payloads.** The service stores and forwards the payload JSON as the publisher wrote it, so it does not need the publishers' payload types. The SignalR notifier pushes the JSON as it is, and the REST API returns a type the service does not know as an [unsupported payload](defining-notifications.md#evolving-a-payload), with the original JSON kept. Register a payload type in the service only when its email or push [content providers](email.md#email-content) must render it.
* **Queues.** Only a process with `Dignite.Abp.Notifications` registers the distribution job and handles the publish requests and the deliveries. A publisher with `Client` consumes neither.

### Dynamic Permissions and Features

**The notification service must turn on** ABP's `PermissionManagementOptions.IsDynamicPermissionStoreEnabled` and `FeatureManagementOptions.IsDynamicFeatureStoreEnabled`, as the example above does. A definition's `RequirePermission` or `RequireFeature` usually names a permission or a feature that another service defines, and the service knows those only through ABP's dynamic stores, which are off by default.

Without them, the permission or feature is unknown in the service, its check answers `false` **without an error**, and every recipient of a notification that requires it is dropped: no inbox row, no delivery. Nothing checks this at startup.

For permission checks, the service also needs `Dignite.Abp.Notifications.Identity` and an `IUserRoleFinder`; see [Permission Checks](publishing-notifications.md#permission-checks).

## The Definition Store

The notification service hosts no business module, so it has no definitions of its own. It gets them the way an ABP microservice gets the permissions and features of the other services: every process saves its definitions to shared tables at startup, and the service reads them. The definition store (`Dignite.Abp.Notifications.Domain` and its database packages) follows ABP's permission management module part for part, and `INotificationDefinitionManager` merges the two sources; a definition of the process itself wins over a saved one with the same name.

* **The publishers save.** With the package installed, a publisher saves its definitions at startup, in the background with retries (`SaveStaticNotificationsToDatabase`, on by default). A publisher does not read the store (`IsDynamicNotificationStoreEnabled` is off by default) and checks its own publishes against its own definitions.
* **The service reads.** With `IsDynamicNotificationStoreEnabled` on, the service reloads the definitions when a publisher's save changes them, at most 30 seconds later: the distributed cache holds a common stamp, as for ABP's permissions.
* **A change is published.** When a save inserts or changes definitions, it publishes their names in a `NotificationDefinitionsChangedEto` on the distributed event bus, like ABP's `DynamicPermissionDefinitionsChangedEto`, for other services to react to. Changes to groups only, and deletions, publish nothing.
* **A notification not read yet is retried, not dropped.** A publisher may publish before the service has read its definitions. The service's handler then throws before writing anything, and the event inbox retries the request. With `InboxProcessorFailurePolicy.RetryLater` it backs off (10·2ⁿ seconds, discarded after `InboxProcessorMaxRetryCount`, 10 by default); with ABP's default `Retry` policy, the same event is retried every period and holds back the events behind it.
* **Several services share the tables.** A save never deletes a definition just because this process no longer defines it: it may be another service's. To remove one, list it on the service that defined it, with `NotificationDefinitionStoreOptions.DeletedNotifications` (or `DeletedNotificationGroups` for a whole group). Two services must not define the same notification name.
* **One cache, distinct application names.** A save is skipped when a hash of the definitions matches the one cached for the application (`IApplicationInfoAccessor.ApplicationName`). Every service of the solution needs the same [distributed cache](https://abp.io/docs/latest/framework/fundamentals/caching) and key prefix, a distinct application name, and a real [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider.
* **Display texts by name.** The display texts are stored as localization resource names and keys, and localized in the service from its own resources or, failing that, ABP's external localization store (the Language Management module in a microservice solution). Attributes are stored only when their value is a JSON scalar.

### Database Tables and Collections

The store uses the `NotifDefinitionGroups` and `NotifDefinitions` tables or collections, on the `NotificationCenter` connection string. Their prefix and schema are the static properties of `NotificationDefinitionStoreDbProperties`, `Notif` by default like the Notification Center's; change both together. The tables are host-level: they are not created in a tenant's own database.

* **Entity Framework Core**: The package ships no migrations. The application that owns the database adds the tables to its migration `DbContext` and adds a migration:

  ````csharp
  builder.ConfigureNotificationCenter();
  builder.ConfigureNotificationDefinitionStore();
  ````

* **MongoDB**: The collections and their unique `Name` indexes are created when the context first builds its model; there is nothing to migrate. A MongoDB context of your own calls `modelBuilder.ConfigureNotificationDefinitionStore()` in `CreateModel`. The store saves in a transactional unit of work, as ABP's permission store does, so the database must support transactions (a replica set).

In a data migration environment (`AddDataMigrationEnvironment()`, as a DbMigrator uses), the store neither saves nor reads.

## Behavior Summary

| Process | Situation | Result |
|---|---|---|
| Any | Neither `Dignite.Abp.Notifications` nor `Client` is installed | Each publish logs a warning and is dropped. |
| Any | Both are installed | The local publisher wins, and the process distributes its own notifications. |
| Publisher | A route names an undefined notification | Startup fails. |
| Publisher | A route names a channel the publisher does not host | Not checked: the channel is hosted elsewhere. |
| Publisher | Publishing an undefined notification | Throws. |
| Notification service | A publish request names a notification it does not know | The handler throws before writing anything, and the event inbox retries the request. |
| Notification service | The dynamic permission or feature store is off | Not checked: the recipients of notifications that require another service's permission or feature are all dropped. |

## Why Two Packages

ABP's own distributed providers put both ends in one package: `Volo.Abp.BackgroundJobs.RabbitMQ` both enqueues and consumes, and a process that must not consume turns `AbpBackgroundJobOptions.IsJobExecutionEnabled` off. That does not work for a publisher here, because the switch covers every job of the process, and a publisher still runs jobs of its own. Not registering the distribution job and the event handlers at all is the only way that needs no new mechanism, so the producing side (`Client`) and the consuming side (`Dignite.Abp.Notifications`) are separate packages. Installed together, they behave as one monolith.

## See Also

* [Delivery Guarantees](delivery-guarantees.md)
* [Distributed Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed)
* [Microservice Solution](https://abp.io/docs/latest/solution-templates/microservice)
