# Installation Notes for Notification Center Module

Dignite.NotificationCenter is a DDD inbox/subscriptions backend (read/unread state, a REST API, and
MVC/Angular UI) built on Dignite.Abp.Notifications' event-driven publish/distribute pipeline and
pluggable Notifiers (Emailing, SignalR). Installing this module brings in the core notification
framework — the contracts (`Dignite.Abp.Notifications.Abstractions`) and the in-process implementation
(`Dignite.Abp.Notifications`) — as its underlying delivery layer. A business module references only
`Dignite.Abp.Notifications.Abstractions`. A service that only publishes notifications for another process to
distribute installs `Dignite.Abp.Notifications.Client` instead of this module, and both sides install
`Dignite.Abp.Notifications.EntityFrameworkCore` (or `Dignite.Abp.Notifications.MongoDB`) so that the process serving
the inbox knows the publishers' notification definitions — see
[Split Deployment](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/notifications/split-deployment.md).

## Documentation

For installation, usage, and extension points, see the module's
[documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/notifications/index.md) and
[repository](https://github.com/dignite-projects/abp-modules).
