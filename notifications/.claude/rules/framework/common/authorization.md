---
paths:
  - "**/*Permission*.cs"
  - "**/*AppService*.cs"
  - "**/*Controller*.cs"
---

# ABP Authorization

> **Docs**: https://abp.io/docs/latest/framework/fundamentals/authorization

## Permission Definition
Define permissions in `*.Application.Contracts` project:

```csharp
public static class BookStorePermissions
{
    public const string GroupName = "BookStore";

    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }
}
```

Register in provider:
```csharp
public class BookStorePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var bookStoreGroup = context.AddGroup(BookStorePermissions.GroupName, L("Permission:BookStore"));

        var booksPermission = bookStoreGroup.AddPermission(
            BookStorePermissions.Books.Default, 
            L("Permission:Books"));
        
        booksPermission.AddChild(
            BookStorePermissions.Books.Create, 
            L("Permission:Books.Create"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BookStoreResource>(name);
    }
}
```

## Using Permissions

### Declarative (Attribute)
```csharp
[Authorize(BookStorePermissions.Books.Create)]
public virtual async Task<BookDto> CreateAsync(CreateBookDto input)
{
    // Only users with Books.Create permission can execute
}
```

### Programmatic Check
```csharp
public class BookAppService : ApplicationService
{
    public async Task DoSomethingAsync()
    {
        await CheckPolicyAsync(BookStorePermissions.Books.Edit);
        
        if (await IsGrantedAsync(BookStorePermissions.Books.Delete))
        {
            // Has permission
        }
    }
}
```

### Allow Anonymous Access
```csharp
[AllowAnonymous]
public virtual async Task<BookDto> GetPublicBookAsync(Guid id)
{
    // No authentication required
}
```

## Current User
Access authenticated user info via `CurrentUser` property (available in base classes like `ApplicationService`, `DomainService`, `AbpController`):

```csharp
public class BookAppService : ApplicationService
{
    public async Task DoSomethingAsync()
    {
        var userId = CurrentUser.Id;
        var isAuthenticated = CurrentUser.IsAuthenticated;
    }
}
```

## Multi-Tenancy Permissions
Control permission availability per tenant side:

```csharp
bookStoreGroup.AddPermission(
    BookStorePermissions.Books.Default,
    L("Permission:Books"),
    multiTenancySide: MultiTenancySides.Tenant // Only for tenants
);
```

## Security Best Practices
- Never trust client input for user identity
- Use `CurrentUser` property (from base class) or inject `ICurrentUser`
- Validate ownership in application service methods
- Filter queries by current user when appropriate
- Don't expose sensitive fields in DTOs

---

## In this repo: two separate permission layers — don't conflate them

1. **Standard ABP permissions** (above) gate `NotificationCenter`'s own AppServices/Controllers —
   e.g. an admin-only "manage all subscriptions" endpoint uses `[Authorize(...)]` exactly like any
   other ABP module.
2. **`INotificationPermissionChecker`** (in Core, `Dignite.Abp.Notifications`) is a separate,
   pluggable abstraction that gates whether a *given user* is allowed to **receive** a given
   notification definition — checked during distribution (`NotificationDefinitionManager` /
   `DefaultNotificationDistributor`), not on an AppService call. The default is
   `AlwaysGrantedNotificationPermissionChecker`; `Notifications.Identity` supplies a real
   implementation backed by ABP Identity/Authorization.

When adding a new notification type that should be permission-gated, wire it through
`INotificationDefinitionProvider`/`NotificationDefinition` (checked via `INotificationPermissionChecker`
at distribution time) — don't try to gate it with an AppService-style `[Authorize]` attribute,
there's no controller action being called at that point.
