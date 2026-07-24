---
paths:
  - "**/*.Application/**/*.cs"
  - "**/Application/**/*.cs"
  - "**/*AppService*.cs"
  - "**/*Dto*.cs"
---

# ABP Application Layer Patterns

> **Docs**: https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services

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

    [Authorize(BookStorePermissions.Books.Create)]
    public async Task<BookDto> CreateAsync(CreateBookDto input)
    {
        var book = new Book(GuidGenerator.Create(), input.Name, input.Price);
        await _bookRepository.InsertAsync(book);
        return MapToDto(book);
    }
}
```

## Application Service Best Practices
- Don't repeat entity name in method names (`GetAsync` not `GetBookAsync`)
- Accept/return DTOs only, never entities
- ID not inside UpdateDto - pass separately
- Call `UpdateAsync` explicitly (don't assume change tracking)
- Don't call other app services in the same module
- Use base class properties (`Clock`, `CurrentUser`, `GuidGenerator`, `L`) instead of injecting these services

## DTO Naming Conventions

| Purpose | Convention | Example |
|---------|------------|---------|
| Query input | `Get{Entity}Input` | `GetUserNotificationListInput` |
| List query input | `Get{Entity}ListInput` | — |
| Single entity output | `{Entity}Dto` | `UserNotificationDto` |
| List item output | `{Entity}ListItemDto` | — |

## DTO Location
- Define DTOs in `*.Application.Contracts` project
- This allows sharing with clients (generated proxies, `HttpApi.Client`)

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
**application rule** (DTO validation, input format) before reaching for `IValidatableObject` or
FluentValidation.

## Error Handling

```csharp
throw new BusinessException("BookStore:010001").WithData("BookName", name);

var book = await _bookRepository.FindAsync(id);
if (book == null) throw new EntityNotFoundException(typeof(Book), id);

throw new UserFriendlyException(L["BookNotAvailable"]);
```

## Auto API Controllers
ABP automatically generates API controllers for application services:
- Interface must inherit `IApplicationService` (which already has `[RemoteService]` attribute)
- HTTP methods determined by method name prefix (Get, Create, Update, Delete)
- Use `[RemoteService(false)]` to disable auto API generation for specific methods

## Object Mapping (Mapperly / AutoMapper)
ABP supports both Mapperly and AutoMapper integrations. **Check which mapper the project you're
touching actually uses before adding one** — see "In this repo" below; don't assume either is
wired up.

### Mapperly (compile-time), if used
```csharp
[Mapper]
public partial class BookMapper
{
    public partial BookDto MapToDto(Book book);
}
```

---

## In this repo

`NotificationCenter.Application`'s `NotificationAppService` does **not** use Mapperly or
AutoMapper — mapping is a hand-written `protected virtual TDto MapToDto(...)` method on the
AppService itself. Follow this (don't introduce a mapper dependency) unless the DTO surface grows
enough to justify one.

The read/inbox side of `NotificationAppService` doesn't touch a repository directly either — it
goes through Core's domain-service-level abstractions (`IUserNotificationManager`,
`INotificationSubscriptionManager`, `INotificationDefinitionManager`), which internally delegate to
`INotificationStore`. When adding an AppService method here, prefer calling these managers over
reaching for `IRepository<T, Guid>` directly, unless the manager genuinely has no suitable method.

Authorization on `NotificationAppService` is a bare class-level `[Authorize]` (any authenticated
user may manage **their own** inbox/subscriptions — enforced by always scoping to
`CurrentUser.GetId()`, not by a fine-grained permission name). This is different from the
generic `[Authorize(SomePermission.Name)]` pattern used for admin-style CRUD — see
`framework/common/authorization.md`. Don't add a permission constant for "read your own inbox";
do add one (and use the declarative pattern) for anything that touches *other* users' data.

Display text (`NotificationDisplayName`) is localized **at read time**, per the current reader's
culture, inside `MapToDto` — not baked in at publish time. Keep this if you touch that method; the
opposite (localizing once at publish/distribution time) was a real bug in the legacy
implementation because background-job distribution runs without a
request culture.
