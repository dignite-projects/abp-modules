```json
//[doc-seo]
{
    "Description": "Route notifications to SignalR, email, push or your own channels with NotificationRoutingOptions: module defaults, host overrides and inbox-only notifications."
}
```

# Routing to Channels

Which channels carry a notification is **not** part of its definition. The module that defines a notification does not know which channels the application installed, so routing is configured separately, in `NotificationRoutingOptions`: a module gives defaults for its own notifications, and the application overrides them.

## Configuring the Routes

````csharp
// In a business module, which references Dignite.Abp.Notifications.Abstractions only
Configure<NotificationRoutingOptions>(options =>
{
    options.ForNotifications(
        ["MyProject.OrderShipped", "MyProject.OrderCancelled"],
        "SignalR", "Push");
});

// In the application's module, which is loaded last
Configure<NotificationRoutingOptions>(options =>
{
    options.Default = [SignalRNotifier.ChannelName];
    options.ForNotification("MyProject.OrderCancelled", SignalRNotifier.ChannelName, EmailNotifier.ChannelName);
    options.InboxOnly("MyProject.AuditEntryAdded");
});
````

* `ForNotification(name, channels...)` and `ForNotifications(names, channels...)` set the channels of notifications. They need at least one channel.
* `InboxOnly(names...)` is the explicit rule for "no channel": the notification is stored in the inbox, and nothing delivers it.
* `Default` holds the channels of the notifications without a rule of their own.

The rules are written in module order, and **later writes win**: a second rule for the same notification replaces the first one as a whole, without merging the channels. That is how the application, configured last, overrides a module's defaults.

Channel names are trimmed and compared case-insensitively; notification names are case-sensitive. The built-in channels are `SignalR` (`SignalRNotifier.ChannelName`), `Email` (`EmailNotifier.ChannelName`) and `Push` (`PushNotifier.ChannelName`), but a channel name is just a string: a business module writes the string rather than reference a channel package, and the routes can name a channel whose notifier runs in another process.

The routes can also be bound from configuration with the standard [options](https://abp.io/docs/latest/framework/fundamentals/options) binding. The keys of the `Notifications` dictionary are notification names:

````json
{
  "NotificationRouting": {
    "Default": [ "SignalR" ],
    "Notifications": {
      "MyProject.OrderCancelled": [ "SignalR", "Email" ]
    }
  }
}
````

````csharp
Configure<NotificationRoutingOptions>(configuration.GetSection("NotificationRouting"));
````

## Notifications Without a Route

A notification with no rule, when `Default` is not set either, is **inbox-only**: it is stored for its recipients, but no channel delivers it. `Default` is never "every installed channel", so installing a new channel package never sends the existing notifications to it silently.

## Startup Checks

The routes are checked once the definitions are loaded, against the definitions of the application itself:

| Situation | Result |
|---|---|
| A rule names a notification that is not defined | Startup fails, listing every unknown name. |
| A rule or `Default` names a channel no notifier in this application hosts | A warning per channel, listing the notifications that use it. Set `RequireHostedChannels = true` to fail instead. |
| There is no Notification Center, and a notification resolves to no channel | Startup fails, listing the notifications: without an inbox, such a notification would go nowhere. |

The last two checks run only where `Dignite.Abp.Notifications` is installed. A [remote publisher](split-deployment.md) routes to channels that another process hosts, which is why an unhosted channel is a warning, not an error: at runtime, the processes that do not host a channel simply ignore its deliveries.

## Routing in Code

For routes that depend on the tenant, the severity or a setting, replace `INotificationChannelResolver`. It is called once per notification, before the recipients are resolved, so it decides for all recipients at once and cannot express per-user preferences.

## See Also

* [SignalR Notifier](signalr.md)
* [Email Notifier](email.md)
* [Push Notifier](push.md)
* [Writing Your Own Notifier](custom-notifiers.md)
