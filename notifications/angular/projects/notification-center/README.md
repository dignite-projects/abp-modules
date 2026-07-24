# @dignite/ng.notification-center

Angular UI library for the **Dignite Notification Center** — a notification **bell** (unread
badge + dropdown) and a **subscriptions** management component, plus the ABP-generated API proxies
(`UserNotificationService` + `NotificationSubscriptionService` + DTOs/enums) over `/api/notification-center`. It mirrors the module's MVC UI for
Angular consumers and is packaged like ABP's own `@abp/ng.*` libraries: a main entry point plus a
`/config` secondary entry point.

```bash
npm install @dignite/ng.notification-center@10.0.0-rc.3
```

The npm package version stays in lockstep with the repository's NuGet package version.

## Entry points

| Import | Contents |
|---|---|
| `@dignite/ng.notification-center` | `NotificationBellComponent` (`<abp-notification-bell>`), `NotificationSubscriptionsComponent`, and the ABP-generated inbox + subscription API proxies (`UserNotificationService`, `NotificationSubscriptionService`) + DTOs/enums. |
| `@dignite/ng.notification-center/config` | `provideNotificationCenterConfig()` — registers the navigation-menu entry into the host — plus the `eNotificationCenterRouteNames` route-name enum. Call the provider once in `app.config.ts`. |

## Usage

Register the menu contribution in the host's `app.config.ts` (mirrors `provideIdentityConfig()` etc.):

```ts
import { provideNotificationCenterConfig } from '@dignite/ng.notification-center/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...ABP providers...
    provideNotificationCenterConfig(),
  ],
};
```

When the SignalR hub is not under the same remote service URL as the generated Notification Center
REST proxy, configure it once with the same provider. `hubPath` is appended to the
`NotificationCenter` remote API URL; `hubUrl` is a fully resolved override:

```ts
provideNotificationCenterConfig({
  realtime: {
    hubPath: '/signalr-hubs/notifications',
    // hubUrl: 'https://notifications.example.com/signalr-hubs/notifications',
  },
});
```

Use the subscriptions component in a page:

```ts
import { NotificationSubscriptionsComponent } from '@dignite/ng.notification-center';

@Component({
  imports: [NotificationSubscriptionsComponent],
  template: `
    <abp-notification-subscriptions />
  `,
})
export class MyNotificationsPage {}
```

The toolbar bell is registered by `provideNotificationCenterConfig()`. It loads its initial count/list
once, then listens to the application-scoped `NotificationRealtimeService`. Multiple bells or pages share
one SignalR connection; mounting and destroying components only reference-counts that shared runtime and
cannot accumulate duplicate `ReceiveNotification` handlers. SignalR is only a prompt: `ReceiveNotification`,
reconnect, token renewal, login/logout, and tenant/account changes all invalidate the bell and trigger
authoritative REST refreshes for unread count/list state. The hub endpoint follows ABP's SignalR convention
and is mapped server-side by ABP. Angular uses the Microsoft SignalR client, as ABP's SignalR documentation
directs non-MVC clients to do.

Advanced hosts can inject `NotificationRealtimeService` directly to observe `refreshRequested$` and
`lifecycle$`, or call `requestRefresh()` after local user actions that should resynchronize notification UI.

### Custom notification bodies and entity links

The bell keeps the notification title and time in a fixed header row. The body below it is rendered by
the notification data discriminator; register custom body components with `NotificationDataComponentsService`.

Notifications are not required to be navigable. Clicking an item marks it as read in place, updates the badge,
and keeps the item visible in the currently open dropdown. The next time the bell is opened, it reloads the
unread list and read items naturally drop out. If a resolver is registered for the item's `entityTypeName`,
the bell marks it as read and navigates through Angular Router so the SPA is not reloaded:

```ts
import { inject, provideAppInitializer } from '@angular/core';
import { NotificationEntityLinksService } from '@dignite/ng.notification-center';

provideAppInitializer(() => {
  inject(NotificationEntityLinksService).register('MyApp.Order', notification => [
    '/orders',
    notification.entityId,
  ]);
});
```

Return a Router commands array, a `UrlTree`, or an app-relative URL string. External URLs still use normal
browser navigation.

## Structure

The `config` entry follows ABP's own convention (see `@abp/ng.identity/config`):

```
config/src/
├── enums/route-names.ts   # eNotificationCenterRouteNames (menu name = localization key)
├── providers/
│   ├── route.provider.ts                     # routes
│   ├── nav-item.provider.ts                  # toolbar bell
│   ├── setting-tab.provider.ts               # subscriptions settings tab
│   └── notification-center-config.provider.ts # provideNotificationCenterConfig()
└── public-api.ts
```

## The proxy is generated — don't hand-edit

Everything under `src/lib/proxy/` is produced by
`abp generate-proxy -t ng -m notification-center -s Host --target notification-center -a NotificationCenter`
(run against a running backend). Hand-written code (components) lives **outside** `proxy/` so it
survives regeneration.

## Building

From the `angular/` workspace root:

```bash
ng build notification-center
```

This emits both entry points — `dist/notification-center` (main) and `dist/notification-center/config`.

During local development the demo app consumes this library **from source**: the TypeScript path
aliases in `angular/tsconfig.json` point at `src/public-api.ts` (and `config/src/public-api.ts`), so
you don't need to pre-build the library or set up any symlinks to see your edits — just `ng serve`
the host app.

### Publishing

Tagged repository releases build, smoke-test, and publish this package automatically. Pre-releases
use npm's `next` dist-tag and stable releases use `latest`. npm requires every package to have a
`latest` tag, so the initial pre-release temporarily owns both tags until the first stable release.
Do not publish a different npm version manually. For a local package inspection, run
`npm pack ./dist/notification-center` from the `angular/` workspace after building.
