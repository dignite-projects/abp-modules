```json
//[doc-seo]
{
    "Description": "The MVC / Razor Pages UI of the Dignite Notification Center: the notification bell in the toolbar, the inbox page, the subscription settings and custom notification rendering."
}
```

# MVC UI

The `Dignite.NotificationCenter.Web` package is the Notification Center's UI for [MVC / Razor Pages](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/overall) applications:

* A **notification bell** in the main toolbar, with the unread count and the latest notifications, updated in real time over SignalR.
* An **inbox page** at `/NotificationCenter/Notifications`.
* A **Subscriptions** tab on the [settings page](https://abp.io/docs/latest/modules/setting-management) of the user.

## Installation

Install the `Dignite.NotificationCenter.Web` package into your `Web` project and depend on `AbpNotificationCenterWebModule`. The bell talks to the [REST API](notification-center.md#rest-api) and listens to the [SignalR notifier](signalr.md), so the application also needs `AbpNotificationCenterHttpApiModule` and `AbpNotificationsSignalRModule`.

Then add the bell's script and style, and ABP's SignalR client, to the global bundles of your theme, in the `ConfigureServices` method of your `Web` module:

````csharp
Configure<AbpBundlingOptions>(options =>
{
    options.StyleBundles.Configure(
        LeptonXLiteThemeBundles.Styles.Global,
        bundle =>
        {
            bundle.AddFiles("/Dignite/NotificationCenter/Web/notification-center.css");
        });

    options.ScriptBundles.Configure(
        LeptonXLiteThemeBundles.Scripts.Global,
        bundle =>
        {
            bundle.AddContributors(typeof(SignalRBrowserScriptContributor));
            bundle.AddFiles("/Dignite/NotificationCenter/Web/notification-center.js");
        });
});
````

`SignalRBrowserScriptContributor` comes from the `Volo.Abp.AspNetCore.Mvc.UI.Packages` package and needs the `@abp/signalr` npm package in your `Web` project; run `abp install-libs` after adding it. Use the global bundle names of your own theme if it is not LeptonX Lite. See [Bundling & Minification](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/bundling-minification).

> Without the SignalR notifier or the SignalR client, the bell still works, without live updates: it shows what the REST API returned when the page loaded.

## The Notification Bell

The bell shows the unread count and the latest notifications, read and unread, with a link to the inbox page. It opens a SignalR connection to the hub and **refreshes from the REST API** whenever a notification arrives or the connection reconnects, because SignalR does not replay missed notifications and the inbox is the authoritative source.

## The Inbox Page

The inbox page lists the user's notifications under group tabs with unread counts, with an all/unread filter, paging, deleting one notification, "mark all as read" and "clear read". Clicking a notification marks it as read and follows its [entity link](#entity-links); the notification stays listed until the user deletes it.

The page has no menu item; users reach it from the bell. Its URL is `NotificationCenterWebConsts.InboxPageUrl`, if you want to link to it.

## Customizing the UI

The UI is configured with `NotificationCenterWebOptions`:

````csharp
Configure<NotificationCenterWebOptions>(options =>
{
    options.SignalRHubUrl = "https://notifications.example.com/signalr-hubs/notifications";

    options.DataViewComponents["MyProject.OrderShipped"] = typeof(OrderShippedNotificationViewComponent);

    options.EntityLinkResolvers["MyProject.Order"] = orderId => $"/Orders/Detail/{orderId}";
});
````

* `SignalRHubUrl` (default `/signalr-hubs/notifications`): The hub the bell connects to. Set an absolute URL when the hub runs on another server; the REST API stays the source of the inbox.
* `DataViewComponents`: Renders the body of a notification with a [view component](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/widgets) of your own, by the **payload type name**. The view component is invoked with a single `data` argument, the item's `NotificationData`.
* `EntityLinkResolvers`: Makes notifications about an entity type clickable, by the **entity type name**. The resolver maps the entity id to a URL of your application.

Both the bell and the inbox page use these options. A notification without a registered view component is rendered on the default card, with its message for the built-in payloads, and its image when the payload implements `IHasNotificationImageUrl`.

### Entity Links

A link is resolved by the UI, not stored with the notification, so the same notification can link to an MVC page in one application and to an Angular route in another. A notification without a resolver for its entity type is not navigable; clicking it only marks it as read.

## See Also

* [Angular UI](angular-ui.md)
* [Notification Center](notification-center.md)
