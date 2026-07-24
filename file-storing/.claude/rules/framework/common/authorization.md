---
paths:
  - "**/*Permission*.cs"
  - "**/*AppService*.cs"
  - "**/*Controller*.cs"
  - "**/*Authorization*.cs"
---

# ABP Authorization

> **Docs**: https://abp.io/docs/latest/framework/fundamentals/authorization

## Permission Definition
Define permissions in `*.Application.Contracts` project:

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

Register in provider (`FileExplorerPermissionDefinitionProvider`):
```csharp
public override void Define(IPermissionDefinitionContext context)
{
    var group = context.AddGroup(FileExplorerPermissions.GroupName, L("Permission:FileExplorer"));

    var files = group.AddPermission(FileExplorerPermissions.Files.Default, L("Permission:Files"));
    files.AddChild(FileExplorerPermissions.Files.Management, L("Permission:Files.Management"));
}

private static LocalizableString L(string name)
    => LocalizableString.Create<FileExplorerResource>(name);
```

## Using Permissions

### Declarative (Attribute)
```csharp
[Authorize(FileExplorerPermissions.Files.Management)]
public virtual async Task DeleteByEntityIdAsync(string containerName, string entityId) { /* ... */ }
```

### Programmatic Check
```csharp
await CheckPolicyAsync(FileExplorerPermissions.Files.Management);

if (await IsGrantedAsync(FileExplorerPermissions.Files.Management)) { /* ... */ }
```

### Allow Anonymous Access
```csharp
[AllowAnonymous]
public virtual async Task<FileDescriptorDto> GetPublicAsync(Guid id) { /* ... */ }
```

## Current User
```csharp
var userId = CurrentUser.Id;
var isAuthenticated = CurrentUser.IsAuthenticated;
```

## Multi-Tenancy Permissions
```csharp
group.AddPermission(
    FileExplorerPermissions.Files.Management,
    L("Permission:Files.Management"),
    multiTenancySide: MultiTenancySides.Tenant);
```

## Security Best Practices
- Never trust client input for user identity
- Use `CurrentUser` property (from base class) or inject `ICurrentUser`
- Validate ownership in application service methods
- Filter queries by current user when appropriate
- Don't expose sensitive fields in DTOs

---

## In this repo: two separate authorization layers — don't conflate them

1. **Standard ABP permissions** (above) gate coarse, admin-style actions —
   `FileExplorerPermissions.Files.Management` is the "manage anyone's files" permission checked with
   `[Authorize(...)]`/`CheckPolicyAsync(...)`.

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
   entity (e.g. "can this user edit the product this image belongs to?"). This is the file-storing analogue
   of a pluggable permission checker — the module ships the seam, the host supplies the policy.

### When gating a new file operation
- Container-wide access rules → set the permission names on the container's
  `BlobContainerAuthorizationConfiguration`, don't hard-code an `[Authorize]` on the handler path.
- Per-associated-entity rules → implement `IFileDescriptorEntityAuthorizationHandler` in the host and point
  the container's config at it.
- **Authorize every resource in a batch.** `DeleteByEntityIdAsync`-style bulk paths must run the
  resource-based check per file, not just the management permission once — skipping that was a real
  bypass (see the Authorization tests). Creating a temporary entity and setting `CreatorId` to the current
  user *before* the create check effectively makes the uploader the owner and neuters the create permission;
  don't reintroduce that ordering.
