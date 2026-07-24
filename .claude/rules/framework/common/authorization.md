---
paths:
  - "**/*Permission*.cs"
  - "**/*AppService*.cs"
  - "**/*Controller*.cs"
  - "**/*Authorization*.cs"
---

# ABP Authorization

> **Docs**: https://abp.io/docs/latest/framework/fundamentals/authorization
>
> Generic ABP conventions first, then a per-module section. **Both modules layer a second,
> non-`[Authorize]` authorization mechanism on top of what's described here** — read your module's
> section before gating anything.

## Permission Definition
Define permissions in the `*.Application.Contracts` project:

```csharp
public static class BookStorePermissions
{
    public const string GroupName = "BookStore";

    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
    }
}
```

Register in a provider:
```csharp
public class BookStorePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(BookStorePermissions.GroupName, L("Permission:BookStore"));

        var books = group.AddPermission(BookStorePermissions.Books.Default, L("Permission:Books"));
        books.AddChild(BookStorePermissions.Books.Create, L("Permission:Books.Create"));
    }

    private static LocalizableString L(string name)
        => LocalizableString.Create<BookStoreResource>(name);
}
```

## Using Permissions

### Declarative (Attribute)
```csharp
[Authorize(BookStorePermissions.Books.Create)]
public virtual async Task<BookDto> CreateAsync(CreateBookDto input) { /* ... */ }
```

### Programmatic Check
```csharp
await CheckPolicyAsync(BookStorePermissions.Books.Edit);

if (await IsGrantedAsync(BookStorePermissions.Books.Delete)) { /* ... */ }
```

### Allow Anonymous Access
```csharp
[AllowAnonymous]
public virtual async Task<BookDto> GetPublicAsync(Guid id) { /* ... */ }
```

## Current User
```csharp
var userId = CurrentUser.Id;
var isAuthenticated = CurrentUser.IsAuthenticated;
```

Available as a property on base classes (`ApplicationService`, `DomainService`, `AbpController`); inject
`ICurrentUser` elsewhere.

## Multi-Tenancy Permissions
```csharp
group.AddPermission(
    BookStorePermissions.Books.Default,
    L("Permission:Books"),
    multiTenancySide: MultiTenancySides.Tenant); // Only for tenants
```

## Security Best Practices
- Never trust client input for user identity
- Use `CurrentUser` property (from base class) or inject `ICurrentUser`
- Validate ownership in application service methods
- Filter queries by current user when appropriate
- Don't expose sensitive fields in DTOs

## Both modules have a second authorization layer — don't conflate them

`[Authorize(...)]`/`CheckPolicyAsync(...)` on an AppService is only *one* of the two mechanisms in each module.
The second one is not an ABP permission at all, and it is where the real access decisions happen:

| Module | Second layer | Decided where |
|---|---|---|
| `file-storing` | **Resource-based authorization** per `FileDescriptor`/`DirectoryDescriptor`, gating permission read **per blob container**, plus a pluggable `IFileDescriptorEntityAuthorizationHandler` | On each individual resource, at access time |
| `notifications` | **`INotificationPermissionChecker`** — gates whether a *given user* may **receive** a given notification definition | During distribution, with no controller action in play |

In both cases, reaching for an `[Authorize]` attribute is the wrong tool for the second layer.

---

## In `file-storing`

### This module's permissions
```csharp
public static class FileExplorerPermissions
{
    public const string GroupName = "FileExplorer";

    public static class Files
    {
        public const string Default = GroupName + ".File";
        public const string Management = Default + ".Management";
    }
}
```
Registered in `FileExplorerPermissionDefinitionProvider`, localized through `FileExplorerResource`.

### The two layers
1. **Standard ABP permissions** gate coarse, admin-style actions —
   `FileExplorerPermissions.Files.Management` is the "manage anyone's files" permission.

2. **Resource-based authorization** decides access to an **individual** `FileDescriptor` /
   `DirectoryDescriptor`. `FileDescriptorAuthorizationHandler` and `DirectoryDescriptorAuthorizationHandler`
   are ASP.NET Core `AuthorizationHandler<OperationAuthorizationRequirement, TResource>` implementations
   (`CommonOperations.Get/Create/Update/Delete`). Which **permission** gates each operation is read
   **per blob container** from `BlobContainerAuthorizationConfiguration`
   (`GetFilePermissionName` / `CreateFilePermissionName` / `UpdateFilePermissionName` /
   `DeleteFilePermissionName`). A file is authorized when **any** of these hold:
   - no permission is configured **and** the operation is `Get` (public-read containers), or
   - the caller is the resource's `CreatorId`, or
   - the container's configured permission is granted, or
   - the global `FileExplorerPermissions.Files.Management` is granted.

   On top of that, a container may register a **pluggable per-entity handler**
   `IFileDescriptorEntityAuthorizationHandler` (`authorizationConfiguration.FileEntityAuthorizationHandler`).
   When a file is associated with a business entity (`EntityId`), that handler's
   `CheckAsync(fileDescriptor, requirement)` runs so the **consuming app** can authorize against its own
   entity (e.g. "can this user edit the product this image belongs to?"). The module ships the seam, the host
   supplies the policy.

### When gating a new file operation
- Container-wide access rules → set the permission names on the container's
  `BlobContainerAuthorizationConfiguration`, don't hard-code an `[Authorize]` on the handler path.
- Per-associated-entity rules → implement `IFileDescriptorEntityAuthorizationHandler` in the host and point the
  container's config at it.
- **Authorize every resource in a batch.** `DeleteByEntityIdAsync`-style bulk paths must run the resource-based
  check per file, not just the management permission once — skipping that was a real bypass (see the
  Authorization tests). Creating a temporary entity and setting `CreatorId` to the current user *before* the
  create check effectively makes the uploader the owner and neuters the create permission; don't reintroduce
  that ordering. See `file-storing-invariants.md` §5.
- **`ContainerNameValidator` must actually validate** — an empty/no-op validator plus permissive defaults means
  an unregistered container is reachable by anyone who can guess a blob name.

---

## In `notifications`

### The two layers
1. **Standard ABP permissions** gate `NotificationCenter`'s own AppServices/Controllers — e.g. an admin-only
   "manage all subscriptions" endpoint uses `[Authorize(...)]` exactly like any other ABP module.

2. **`INotificationPermissionChecker`** (in Core, `Dignite.Abp.Notifications`) is a separate, pluggable
   abstraction that gates whether a *given user* is allowed to **receive** a given notification definition —
   checked during distribution (`NotificationDefinitionManager` / `DefaultNotificationDistributor`), not on an
   AppService call. The default is `AlwaysGrantedNotificationPermissionChecker`; `Notifications.Identity`
   supplies a real implementation backed by ABP Identity/Authorization.

When adding a new notification type that should be permission-gated, wire it through
`INotificationDefinitionProvider`/`NotificationDefinition` — don't try to gate it with an AppService-style
`[Authorize]` attribute; there's no controller action being called at that point.

### Definition requirements apply again at delivery
`NotificationDefinition.PermissionName` and `FeatureName` constrain both subscription **and** delivery. **Never
treat an explicit `userIds` array as an implicit authorization or eligibility bypass** — explicit and
subscription-derived candidates run through the same `INotificationDefinitionManager.IsAvailableAsync` filter in
the distributor, after caller-supplied exclusions and before inbox persistence or channel publication.

Gating is simply "don't set `PermissionName`/`FeatureName` if you don't want it." Don't reintroduce a
replaceable eligibility-evaluator contract or a requirement-bypassing publish API — both were built and removed
as over-engineering. See `notifications-invariants.md` §7.

### AppService-level authorization is a bare `[Authorize]`
Any authenticated user may manage **their own** inbox/subscriptions — enforced by always scoping to
`CurrentUser.GetId()`, not by a fine-grained permission name. Don't add a permission constant for "read your own
inbox"; **do** add one for anything that touches *other* users' data.
