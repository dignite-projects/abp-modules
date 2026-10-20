```json
//[doc-seo]
{
    "Description": "Push notifications to iOS and Android devices with the push notifier of the Dignite Notifications module, the Expo provider and the Notification Center's device registry."
}
```

# Push Notifier

The push notifier sends notifications to the recipient's **phones**. It is one channel, `Push` (`PushNotifier.ChannelName`), whatever service delivers the message: each registered device names the **push provider** that issued its token, so a route only ever names `Push`. The module ships a provider for the [Expo Push Service](https://docs.expo.dev/push-notifications/overview/), which delivers to iOS and Android through one API.

## Installation

Install `Dignite.Abp.Notifications.Push.Expo`, which brings the `Dignite.Abp.Notifications.Push` channel with it, into the project that delivers the notifications. With the Notification Center, also install `Dignite.NotificationCenter.Push`, which provides the devices:

````csharp
[DependsOn(
    typeof(AbpNotificationsPushExpoModule),        // the Push channel and the Expo provider
    typeof(AbpNotificationCenterPushModule)        // devices from the Notification Center's registry
    // typeof(AbpNotificationCenterPushIdentityModule) // optional, see below
)]
public class MyProjectModule : AbpModule
{
}
````

Then [route](routing.md) notifications to the `Push` channel.

> The Push package alone has **no devices** to push to, so nothing is sent (and a warning is logged) until a device store exists. Without the Notification Center, implement `IPushDeviceStore` over your own device storage; see [Devices Without the Notification Center](#devices-without-the-notification-center).

## Configuring Expo

````csharp
Configure<ExpoPushOptions>(options =>
{
    options.AccessToken = configuration["Expo:AccessToken"];
});
````

| Option | Default | Description |
|---|---|---|
| `AccessToken` | `null` | The access token of your Expo project. Keep it in secret configuration, never in source control. |
| `BaseAddress` | `https://exp.host` | The address of the Expo Push Service. |
| `Sound` | `default` | The sound of the notification; `null` for none. |
| `Priority` | `high` | The delivery priority. |
| `AndroidChannelId` | `null` | The Android notification channel to show the notification in. |

> **Turn on Enhanced Security for Push Notifications** in your Expo project and set `AccessToken` in production. Without it, anyone who holds a device's Expo push token can send pushes to that device.

## Registering Devices

With the Notification Center, the app registers the device it runs on through the [REST API](notification-center.md#rest-api), and the Notification Center keeps one `PushDevice` per app installation:

* **Register** (`POST /api/notification-center/push-devices/register`, with `{ "provider": "Expo", "token": "..." }`) on every launch, after sign-in, and when the user changes the app's language. The device's language is the culture of the request, so send an `Accept-Language` header. A token already registered to another user, in any tenant, moves to the caller: whoever signed in last on a phone gets its pushes.
* **Unregister** (`POST /api/notification-center/push-devices/unregister`, with the same body) at sign-out, **before** revoking the access token, because the call needs it. It only ever removes the caller's own device.

A user keeps at most `PushDeviceOptions.MaxDevicesPerUser` devices (10 by default); registering one more forgets the device seen least recently. Together with the devices a provider reports as dead, which are removed, this keeps the registry bounded without a cleanup job.

The token is unique across tenants, so a phone that moves to another tenant stops receiving the previous tenant's pushes. With a database per tenant this cannot be enforced across databases, and the guarantee rests on the app unregistering at sign-out.

### Following the Login Session

`Dignite.NotificationCenter.Push.Identity` (`AbpNotificationCenterPushIdentityModule`) makes push follow the user's ABP login session: a device registered under a session that no longer exists, because the user signed out, the session was revoked, ended by the concurrent login rule or cleaned up as inactive, is forgotten instead of pushed to. That also covers sign-outs the app could not report. It needs an application that keeps `IdentitySession` records and issues the `session_id` claim, as the session management of ABP's Identity Pro module does; elsewhere devices carry no session, and the package does nothing.

## Push Content

The text of a push is built once per device culture by a chain of `INotificationPushContentProvider` services, which works like the [email content](email.md#email-content) chain. Derive from `NotificationPushContentProvider<TData>`:

````csharp
public class OrderShippedPushContentProvider
    : NotificationPushContentProvider<OrderShippedNotificationData>, ITransientDependency
{
    private readonly IStringLocalizer<MyProjectResource> _localizer;

    public OrderShippedPushContentProvider(IStringLocalizer<MyProjectResource> localizer)
    {
        _localizer = localizer;
    }

    protected override Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        OrderShippedNotificationData data,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<NotificationPushContent?>(new NotificationPushContent(
            title: _localizer["Push:OrderShipped:Title"],
            body: _localizer["Push:OrderShipped:Body"]));
    }
}
````

The built-in providers handle `MessageNotificationData` and `LocalizableMessageNotificationData`. A device without a culture uses `NotificationPushOptions.DefaultCulture` (`en` by default).

> Push text travels through Apple's, Google's and the provider's servers, and shows on a lock screen. Keep it to "something new, open the app", and leave the details to the app.

Every push also carries the following values as silent data, under the keys of the `PushDataKeys` class, so a tapped notification can open its inbox entry and mark it as read: `notificationId`, `notificationName`, `entityTypeName` and `entityId`.

## Delivery

Delivery is best-effort, like every channel. Expo push **receipts** are not polled, so there is no delivery state; only devices the provider reports as no longer registered on the push ticket are removed.

## Devices Without the Notification Center

Implement `IPushDeviceStore`, and register it in place of the default null store:

````csharp
public class MyPushDeviceStore : IPushDeviceStore, ITransientDependency
{
    public Task<IReadOnlyList<PushTarget>> GetTargetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Return the devices of the user: provider name, token and culture
    }

    public Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
    {
        // Called when the provider reports a device as dead
    }
}
````

Register it with `[Dependency(ReplaceServices = true)]`, or with `context.Services.Replace(...)`.

## Other Push Services

A push provider implements `IPushProvider`: a `Name`, which devices are registered with, and a `SendAsync` method that sends a batch of `PushMessage`s and returns a `PushSendResult` for each token (`Succeeded`, `TokenInvalid` or `Failed`). A `TokenInvalid` result removes the device. Register it as a transient service; the notifier picks the provider by the name each device carries.

## See Also

* [Notification Center](notification-center.md)
* [Routing to Channels](routing.md)
