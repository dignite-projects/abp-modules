# Installation Notes for Notification Center Module

Dignite.NotificationCenter is a DDD inbox/subscriptions backend (read/unread state, a REST API, and
MVC/Angular UI) built on Dignite.Abp.Notifications' event-driven publish/distribute pipeline and
pluggable Notifiers (Emailing, SignalR). Installing this module brings in the core notification
framework and its in-process distribution pipeline (`Dignite.Abp.Notifications.Distribution`) as its
underlying delivery layer. A service that only publishes notifications for another process to distribute
installs `Dignite.Abp.Notifications.Remote` instead of this module — see "Split deployment" in the README.

## Documentation

For structure, usage, and extension points, see the module's own
[README](https://github.com/dignite-projects/abp-modules/blob/main/notifications/README.md) and
[repository](https://github.com/dignite-projects/abp-modules).
