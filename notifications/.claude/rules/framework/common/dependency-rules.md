---
paths:
  - "**/*.csproj"
  - "**/*Module*.cs"
---

# Dependency Rules

## Core Principles (Always Apply)

1. **Domain logic never depends on infrastructure** (no `DbContext` in Domain/Application).
2. **Depend on abstractions** (interfaces), not concrete implementations.
3. **Higher layers depend on lower layers**, never the reverse.
4. **Data access through repositories**, not direct `DbContext` — see `framework/data/ef-core.md`
   for how this repo does queries (generic repository + `INotificationStore`, no custom repo
   interfaces).

## This repo's actual dependency graph (two trees)

```
Notifications.Abstractions               (contracts: NotificationData, NotificationDeliveryRequestedEto, registries)
        │
        ▼
Notifications (Core)                     (definitions, publish/distribute, INotificationStore abstraction)
        │
        ├──▶ Notifications.Identity      (permission-checker impl)
        │
        ▼
NotificationCenter.Domain.Shared ──▶ NotificationCenter.Domain
                                              │
                    ┌─────────────────────────┼─────────────────────────┐
                    ▼                          ▼                        ▼
     NotificationCenter.Application.Contracts   EntityFrameworkCore      MongoDB
                    │                    (both implement INotificationStore)
                    ▼
     NotificationCenter.Application ──▶ HttpApi / HttpApi.Client

Notifiers (SignalR / Emailing / future WebPush, FCM, SMS, Webhook):
  depend on Abstractions + their own channel SDK only, no exceptions
  (see notifications-invariants.md §3) — never on Core or NotificationCenter.
```

Central rule specific to this repo: **`NotificationCenter` is an optional application of `Notifications`
(Core), not a prerequisite for it.** Core must work with `NullNotificationStore` alone (see
"Two operation modes" in `template/app.md`).

## Critical rules

### ❌ Never do

```csharp
// Application layer accessing DbContext directly
public class NotificationAppService : ApplicationService
{
    private readonly NotificationCenterDbContext _dbContext; // ❌ WRONG
}

// Core (Notifications) depending on NotificationCenter or a specific persistence provider
// ❌ WRONG — Core must only know about the INotificationStore abstraction

// A Notifier depending on NotificationCenter.Domain / EntityFrameworkCore / MongoDB
// ❌ WRONG — breaks the plugin boundary (notifications-invariants.md §3)
```

### ✅ Always do

```csharp
// Application layer using the generic repository
public class NotificationAppService : ApplicationService
{
    private readonly IRepository<Notification, Guid> _notificationRepository; // ✅
}

// Core depends only on the store abstraction, resolved via DI
public class DefaultNotificationDistributor : INotificationDistributor
{
    private readonly INotificationStore _store; // ✅ Null or Center impl, Core doesn't care which
}
```

## Central package management

**All package versions live in `Directory.Packages.props`** at the repo root
(`ManagePackageVersionsCentrally=true`). A `.csproj` should only have
`<PackageReference Include="..." />` with **no `Version=`** attribute. Adding a new NuGet
dependency or bumping a version means editing `Directory.Packages.props`, not the individual
`.csproj`.

## Multi-targeting for contract layers

`Abstractions`, `Domain.Shared`, `Application.Contracts`, and `HttpApi.Client` projects target
`netstandard2.0;netstandard2.1;net10.0` (so older/remote consumers can reference them);
implementation projects (`Notifications`, `Domain`, `Application`, `EntityFrameworkCore`,
`MongoDB`, `HttpApi`, Notifiers) target `net10.0` only. Keep new contract-layer projects
multi-targeted and implementation projects single-targeted, matching the existing `.csproj`s.

## Enforcement checklist when adding a feature

1. New notification type? → No new project/entity needed — see "Adding a feature" in `template/app.md`.
2. New Notifier (channel)? → New project under `core/src/`, depends on Abstractions only if possible.
3. New aggregate/entity? → `NotificationCenter.Domain` (+ Domain.Shared for constants).
4. New query? → A method on `INotificationStore`, not a new repository interface.
5. New DTO/service interface? → `Application.Contracts`; implementation in `Application`.
6. New package? → Add the version to `Directory.Packages.props`, then reference it (no version) in the `.csproj`.
