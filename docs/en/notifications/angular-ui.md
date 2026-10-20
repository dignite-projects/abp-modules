```json
//[doc-seo]
{
    "Description": "The Angular UI of the Dignite Notification Center: the notification bell, the inbox page, the subscription settings, real-time updates and custom notification rendering."
}
```

# Angular UI

The `@dignite/ng.notification-center` npm package is the Notification Center's UI for [Angular](https://abp.io/docs/latest/framework/ui/angular/overview) applications. It is packaged like ABP's own `@abp/ng.*` packages, with a main entry point and a `/config` entry point, and provides:

* A **notification bell** in the toolbar, with the unread count and the latest notifications, updated in real time over SignalR.
* An **inbox page** at `/notifications`.
* A **Subscriptions** tab on the settings page of ABP's Setting Management module.
* The generated API proxies of the [REST API](notification-center.md#rest-api) (`UserNotificationService`, `NotificationSubscriptionService` and their DTOs).

The server needs the Notification Center's `HttpApi` package, and the [SignalR notifier](signalr.md) for live updates.

## Installation

Install the package with the same version as the NuGet packages of the server:

````bash
npm install @dignite/ng.notification-center
````

Register the bell, the settings tab and the route in `app.config.ts`, as for ABP's own modules (`provideIdentityConfig()` and so on):

````ts
import { provideNotificationCenterConfig } from '@dignite/ng.notification-center/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...ABP providers
    provideNotificationCenterConfig(),
  ],
};
````

Then mount the inbox page's lazy routes at `/notifications`, the path the config registers:

````ts
// app.routes.ts
{
  path: 'notifications',
  loadChildren: () => import('@dignite/ng.notification-center').then(c => c.createRoutes()),
},
````

The proxies use the `NotificationCenter` API name, so add it to the `apis` of your [environment](https://abp.io/docs/latest/framework/ui/angular/environment) when the Notification Center runs on another server than the default API.

## Real-Time Updates

The bell connects to the SignalR hub at `/signalr-hubs/notifications` of the `NotificationCenter` API URL. When the hub is somewhere else, configure it in the same provider: `hubPath` is appended to the `NotificationCenter` API URL, and `hubUrl` is a complete URL that overrides it:

````ts
provideNotificationCenterConfig({
  realtime: {
    hubPath: '/signalr-hubs/notifications',
    // hubUrl: 'https://notifications.example.com/signalr-hubs/notifications',
  },
});
````

SignalR is only a **prompt**. A `ReceiveNotification` message, a reconnect, a token renewal, a login or logout, and a change of tenant or account all make the bell refresh its unread count and list from the REST API, which is the authoritative source. All bells and pages share one application-wide connection, `NotificationRealtimeService`; inject it to observe `refreshRequested$` and `lifecycle$`, or call `requestRefresh()` after a local action that should resynchronize the notification UI.

## The Notification Bell

The bell lists the ten most recent notifications, read and unread, with unread ones highlighted, and a "View all" link to the inbox page. The badge counts only unread notifications. Clicking a notification marks it as read in place; when an [entity link](#entity-links) is registered for it, the bell also navigates there with the Angular Router.

## The Inbox Page

The inbox lists the user's notifications under group tabs with unread counts, with an all/unread filter, paging, deleting one notification, "mark all as read" and "clear read". Clicking a notification marks it as read and follows its entity link; the notification stays listed. The page has no menu item; users reach it from the bell.

Replace the page with ABP's [component replacement](https://abp.io/docs/latest/framework/ui/angular/component-replacement) system, with the `eNotificationCenterComponents.Notifications` key. After the page marks or deletes notifications, it emits `NotificationCenterEventsService.inboxChanged$`, which the bell uses to refresh its badge.

## The Subscriptions Component

`provideNotificationCenterConfig()` adds the subscriptions to the settings page. To show them on a page of your own, use the standalone component:

````ts
import { NotificationSubscriptionsComponent } from '@dignite/ng.notification-center';

@Component({
  imports: [NotificationSubscriptionsComponent],
  template: `<abp-notification-subscriptions />`,
})
export class MyNotificationsPage {}
````

## Customizing the Rendering

### Notification Bodies

The bell and the inbox show a notification's title and time in a fixed header, and render its body by the **payload type name**. Register a standalone component of your own for a payload type with `NotificationDataComponentsService`; the component gets the payload in a `data` input:

````ts
import { inject, provideAppInitializer } from '@angular/core';
import { NotificationDataComponentsService } from '@dignite/ng.notification-center';

provideAppInitializer(() => {
  inject(NotificationDataComponentsService).register('MyProject.OrderShipped', OrderShippedNotificationComponent);
});
````

The built-in payloads have components already. A payload without a component shows only its image, when it has an `imageUrl`.

### Entity Links

Notifications are not required to be navigable. Register a resolver for an entity type name with `NotificationEntityLinksService` to make notifications about it clickable:

````ts
import { inject, provideAppInitializer } from '@angular/core';
import { NotificationEntityLinksService } from '@dignite/ng.notification-center';

provideAppInitializer(() => {
  inject(NotificationEntityLinksService).register('MyProject.Order', notification => [
    '/orders',
    notification.entityId,
  ]);
});
````

A resolver returns Router commands, a `UrlTree` or an application-relative URL string; external URLs use normal browser navigation.

## See Also

* [MVC UI](mvc-ui.md)
* [Notification Center](notification-center.md)
