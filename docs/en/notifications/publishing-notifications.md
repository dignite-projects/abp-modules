```json
//[doc-seo]
{
    "Description": "Publish notifications with INotificationPublisher: recipients, subscribers, entities, severities, background distribution and recipient eligibility checks."
}
```

# Publishing Notifications

`INotificationPublisher` is the one service business code uses to send a notification. It is defined in `Dignite.Abp.Notifications.Abstractions`; which implementation you get depends on what the application installed (see [The Publisher Implementations](#the-publisher-implementations)).

## PublishAsync

````csharp
await _notificationPublisher.PublishAsync(
    "MyProject.OrderShipped",
    new OrderShippedNotificationData { OrderNumber = "SO-1001", ItemCount = 3 },
    entityIdentifier: new NotificationEntityIdentifier("MyProject.Order", order.Id.ToString()),
    severity: NotificationSeverity.Success,
    userIds: [order.CustomerId]);
````

`PublishAsync` gets the following parameters:

* **notificationName** (`string`): The name of a [defined](defining-notifications.md) notification. Publishing an undefined name throws.
* **data** (`NotificationData`, optional): The [payload](defining-notifications.md#payloads).
* **entityIdentifier** (`NotificationEntityIdentifier`, optional): The entity the notification is about, as an entity type name and an id. The UIs use it to link the notification to the entity, and [subscriptions](notification-center.md#subscriptions) can target one entity.
* **severity** (`NotificationSeverity`, default `Info`): `Info`, `Success`, `Warn` or `Error`. The UIs use it to style the notification.
* **userIds** (`Guid[]`, optional): The recipients. See below.
* **excludedUserIds** (`Guid[]`, optional): Users that never receive this notification, such as the user who caused it.

### Recipients

The `userIds` argument decides who receives the notification:

* **`null`**: The **subscribers** of the notification. This needs the [Notification Center](notification-center.md), which stores the subscriptions.
* **An empty array**: Nobody. It is a deliberate no-op, and the call returns immediately.
* **A non-empty array**: Exactly these users. Duplicates are removed.

In both cases, the excluded users are removed, and then every remaining recipient must be eligible; see [Recipient Eligibility](#recipient-eligibility).

### Inline or in the Background

A notification for at most `DirectDistributionUserThreshold` explicit users (5 by default) is distributed **inline**, in the call. A larger one, and every notification for subscribers, is distributed by a [background job](https://abp.io/docs/latest/framework/infrastructure/background-jobs) named `Dignite.Abp.Notifications.Distribute`, so publishing never waits for a large fan-out.

The distributor works through the recipients in batches of `RecipientBatchSize` (256 by default): for each batch, it checks eligibility, writes the inbox rows and publishes the deliveries. Subscribers are read with keyset paging, so subscriptions added or removed during the distribution cannot make it repeat or skip a recipient. Both values are options of the process that distributes:

````csharp
Configure<NotificationDistributionOptions>(options =>
{
    options.DirectDistributionUserThreshold = 10;
    options.RecipientBatchSize = 500;
});
````

Both are validated at startup, and neither can exceed 10,000.

### Multi-Tenancy

`PublishAsync` records the current tenant with the notification. Everything after it, the subscriber lookup, the eligibility checks, the inbox rows and the deliveries, runs in **that tenant**, wherever the background job runs. A notification published in the host context is a host notification. See [Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy).

## Recipient Eligibility

The [requirements](defining-notifications.md#requirements) of a definition decide not only who may subscribe to a notification but also **who receives it**. Every candidate recipient, whether a subscriber or a user you named explicitly, must meet the definition's `RequirePermission` and `RequireFeature` requirements, or it is dropped without an inbox row or a delivery. Naming a user explicitly is **not** a way around the requirements.

This is a delivery policy, not an authorization of the publisher: the code that publishes still needs its own [authorization](https://abp.io/docs/latest/framework/fundamentals/authorization) checks where it matters.

### Permission Checks

The default `INotificationPermissionChecker` grants every user. Install `Dignite.Abp.Notifications.Identity` and depend on `AbpNotificationsIdentityModule` for real permission checks: it asks ABP's `IUserRoleFinder` for the recipient's roles and checks the permission through ABP's `IPermissionChecker`, as the user and role permission providers would for that user.

The application must be able to resolve an `IUserRoleFinder`:

* An application that has the Identity module's database, such as a monolith, already has one.
* A service without it installs an Identity `HttpApi.Client` package (`Volo.Abp.Identity.HttpApi.Client`, or `Volo.Abp.Identity.Pro.HttpApi.Client`) and points `RemoteServices:AbpIdentity` at the Identity service, which must expose its [integration services](https://abp.io/docs/latest/framework/api-development/integration-services). Such a service must also know the permissions and features other services define; see [Split Deployment](split-deployment.md#dynamic-permissions-and-features).

Known limits: there is one role lookup per candidate recipient; a permission with [state checkers](https://abp.io/docs/latest/framework/infrastructure/simple-state-checker) is evaluated against the ambient current user, which nobody is during background distribution; the user's `IsActive` flag is not checked.

## The Publisher Implementations

| Installed | `INotificationPublisher` |
|---|---|
| `Dignite.Abp.Notifications` | The local publisher: distributes in this process. |
| `Dignite.Abp.Notifications.Client` | The remote publisher: sends the notification to another process to distribute. See [Split Deployment](split-deployment.md). |
| Both | The local publisher, whatever the module order. |
| Neither | `NullNotificationPublisher`: logs a warning and drops every notification. |

## Using the Distributor Directly

`INotificationDistributor` is the service behind the publisher. Code that calls it directly, with a `NotificationInfo`, must set `NotificationInfo.TenantId` itself for a tenant notification: the value is authoritative, and `null` means the host, even when the caller is in a tenant.

## See Also

* [Defining Notifications](defining-notifications.md)
* [Routing to Channels](routing.md)
