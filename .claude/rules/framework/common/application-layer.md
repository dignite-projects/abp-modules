---
paths:
  - "**/*.Application/**/*.cs"
  - "**/Application/**/*.cs"
  - "**/*AppService*.cs"
  - "**/*Dto*.cs"
---

# ABP Application Layer Patterns

> **Docs**: https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services
>
> Generic ABP conventions first, then a per-module section. The two modules answer the mapper and
> controller questions differently — read your module's section before adding either.

## Application Service Structure

### Interface (Application.Contracts)
```csharp
public interface IBookAppService : IApplicationService
{
    Task<BookDto> GetAsync(Guid id);
    Task<PagedResultDto<BookListItemDto>> GetListAsync(GetBookListInput input);
    Task<BookDto> CreateAsync(CreateBookDto input);
}
```

### Implementation (Application)
```csharp
public class BookAppService : ApplicationService, IBookAppService
{
    private readonly IBookRepository _bookRepository;

    public BookAppService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<BookDto> GetAsync(Guid id)
    {
        var book = await _bookRepository.GetAsync(id);
        return MapToDto(book);
    }
}
```

## Application Service Best Practices
- Don't repeat the entity name in method names (`GetAsync`, not `GetBookAsync`)
- Accept/return DTOs only, never entities
- ID not inside UpdateDto — pass separately
- Call `UpdateAsync` explicitly (don't assume change tracking)
- Don't call other app services in the same module
- Use base class properties (`Clock`, `CurrentUser`, `GuidGenerator`, `L`) instead of injecting these services
- **Distinguish a full update from a patch.** Overwriting every field unconditionally on a partial update wipes
  metadata the client never sent.

## DTO Naming Conventions

| Purpose | Convention |
|---------|------------|
| Query input | `Get{Entity}Input` |
| List query input | `Get{Entity}ListInput` |
| Create input | `Create{Entity}Input` / `Create{Entity}Dto` |
| Single entity output | `{Entity}Dto` |
| List item output | `{Entity}ListItemDto` |

Each module has settled on one of the `Input`/`Dto` spellings — match the file you're editing.

## DTO Location
- Define DTOs in the `*.Application.Contracts` project
- This allows sharing with clients (generated proxies, `HttpApi.Client`, the Angular proxy)

## Validation

### Data Annotations
```csharp
public class CreateBookDto
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; }
}
```

Decide whether a rule is a **domain rule** (put it in the entity constructor/domain service) or an
**application rule** (DTO shape, input format) before reaching for `IValidatableObject` or FluentValidation.

## Error Handling

```csharp
throw new BusinessException("MyModule:SomethingWentWrong").WithData("Name", name);

var book = await _bookRepository.FindAsync(id);
if (book == null) throw new EntityNotFoundException(typeof(Book), id);

throw new UserFriendlyException(L["SomeUserFacingMessage"]);
```

## Auto API Controllers
ABP can generate API controllers from application services:
- The interface must inherit `IApplicationService` (which already carries `[RemoteService]`)
- The HTTP verb comes from the method-name prefix (Get, Create, Update, Delete)
- Use `[RemoteService(false)]` to opt a method out

**The two modules differ here**: `file-storing` uses conventional (auto) controllers throughout;
`notifications` exposes **explicit** controllers. Check the module before assuming either.

After changing an AppService signature, **regenerate the clients** — the C# proxies in `HttpApi.Client` and the
Angular proxy under `angular/projects/*/src/lib/proxy` both drift otherwise.

## Object Mapping — neither mapper is a repo-wide default

| Module | Mapping |
|---|---|
| `file-storing` | **Mapperly**, compile-time, in `FileExplorerApplicationMappers.cs`. Deliberately moved off AutoMapper — don't reintroduce it. |
| `notifications` | **No mapper at all** — a hand-written `protected virtual TDto MapToDto(...)` on the AppService. Don't introduce a mapper dependency without cause. |

"The other module does it this way" is not a reason to change either one.

---

## In `file-storing`

### DTO naming used here
| Purpose | Convention | Example |
|---------|------------|---------|
| Query input | `Get{Entity}Input` | `GetFileListInput` |
| Create input | `Create{Entity}Input` | `CreateFileInput` |
| Single entity output | `{Entity}Dto` | `FileDescriptorDto`, `DirectoryDescriptorDto` |
| Tree/aggregate output | `{Entity}InfoDto` | `DirectoryDescriptorInfoDto` (carries `Children`) |

### Domain rule vs application rule
Blob-name uniqueness and directory-parent validity are **domain rules** — they live in the entity/domain
service, not in DTO validation.

### Error handling
```csharp
throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist).WithData("Name", name);
```
Keep error-code namespaces aligned with the localization resource keys — see `abp-core.md`.

### Full update vs patch — don't silently clear metadata
Overwriting `DirectoryId`/`Name`/`CellName` unconditionally on a rename wipes fields the client never sent (the
Angular rename sends only `{ name }`). The update path was split into full-update vs patch for exactly this
reason, guarded by `Update.Tests` (`Rename_ShouldPreserveDirectoryAndCellName`). See
`file-storing-invariants.md` §9.

### Auto API controllers
AppServices are exposed as ABP **conventional (auto) API controllers** — there are no hand-written controllers.
`FileExplorerHttpApiModule` registers the application part and tunes the conventions:

```csharp
options.ConventionalControllers.FormBodyBindingIgnoredTypes.Add(typeof(CreateFileInput)); // multipart uploads
```

After changing a signature, regenerate the C# proxies in `HttpApi.Client` **and** the Angular proxy under
`angular/projects/file-explorer/src/lib/proxy` — the audit found real proxy/contract drift.

### Object mapping runs on **Mapperly**, not AutoMapper
Mapping lives in `FileExplorerApplicationMappers.cs`. Each map is a compile-time Mapperly partial extending
ABP's `MapperBase<TSource, TDestination>`:

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FileDescriptorToDtoMapper : MapperBase<FileDescriptor, FileDescriptorDto>
{
    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]   // Url is resolved per-request, not stored
    public override partial FileDescriptorDto Map(FileDescriptor source);
    public override partial void Map(FileDescriptor source, FileDescriptorDto destination);
}
```

- Consume it through ABP's `IObjectMapper`, registered by `AbpMapperlyModule` (a dependency of
  `FileExplorerApplicationModule`).
- `RequiredMappingStrategy.Target` makes any unmapped destination property a **build error**, so every new DTO
  field is either mapped by name or explicitly `[MapperIgnoreTarget]`-ed.
- **Computed, non-stored fields are ignored and filled in the AppService**: `FileDescriptorDto.Url` and
  `DirectoryDescriptorInfoDto.Children` (the directory tree, assembled by `DirectoryDescriptorAppService`).
- The repo was deliberately moved off AutoMapper to keep the published packages clear of AutoMapper's advisory
  and match ABP's own modules. **Don't reintroduce AutoMapper** or a hand-written `MapToDto` switch.

### Authorization is resource-based, not just `[Authorize]`
`FileDescriptorAppService`/`DirectoryDescriptorAppService` authorize **individual resources** through
`IAuthorizationService` + `OperationAuthorizationRequirement`, on top of the coarse
`FileExplorerPermissions.Files.Management` permission. Batch operations must authorize **each** resource — see
`framework/common/authorization.md`.

---

## In `notifications`

### DTO naming used here
| Purpose | Convention | Example |
|---------|------------|---------|
| Query input | `Get{Entity}Input` | `GetUserNotificationListInput` |
| Single entity output | `{Entity}Dto` | `UserNotificationDto` |

### No mapper — mapping is hand-written
`NotificationAppService` does **not** use Mapperly or AutoMapper — mapping is a hand-written
`protected virtual TDto MapToDto(...)` method on the AppService itself. Follow this unless the DTO surface grows
enough to justify a mapper.

### Go through the managers, not the repository
The read/inbox side doesn't touch a repository directly — it goes through Core's domain-service-level
abstractions (`IUserNotificationManager`, `INotificationSubscriptionManager`, `INotificationDefinitionManager`),
which internally delegate to `INotificationStore`. Prefer these managers over reaching for
`IRepository<T, Guid>` directly, unless the manager genuinely has no suitable method.

### Authorization is a bare class-level `[Authorize]`
Any authenticated user may manage **their own** inbox/subscriptions — enforced by always scoping to
`CurrentUser.GetId()`, not by a fine-grained permission name. Don't add a permission constant for "read your own
inbox"; **do** add one (declaratively) for anything touching *other* users' data.

### Explicit controllers, not conventional/auto ones
`HttpApi` exposes explicit controllers under `/api/notification-center` (`UserNotificationController` for the
inbox, `NotificationSubscriptionController` for subscriptions, plus `AddApplicationPartIfNotExists`). Don't
assume ABP auto API controller behaviour here.

### Display text is localized at read time
`NotificationDisplayName` is localized **per the current reader's culture, inside `MapToDto`** — not baked in at
publish time. Keep this if you touch that method; the opposite was a real bug in the legacy implementation,
because background-job distribution runs without a request culture.
