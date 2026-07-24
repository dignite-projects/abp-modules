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
public interface IFileDescriptorAppService : IApplicationService
{
    Task<FileDescriptorDto> GetAsync(Guid id);
    Task<ListResultDto<FileDescriptorDto>> GetListAsync(GetFileListInput input);
    Task<FileDescriptorDto> CreateAsync(CreateFileInput input);
}
```

### Implementation (Application)
```csharp
public class FileDescriptorAppService : ApplicationService, IFileDescriptorAppService
{
    private readonly IFileDescriptorRepository _fileDescriptorRepository;

    public FileDescriptorAppService(IFileDescriptorRepository fileDescriptorRepository)
    {
        _fileDescriptorRepository = fileDescriptorRepository;
    }

    public async Task<FileDescriptorDto> GetAsync(Guid id)
    {
        var file = await _fileDescriptorRepository.GetAsync(id);
        // resource-based authorization + mapping — see below
        return await MapToDtoAsync(file);
    }
}
```

## Application Service Best Practices
- Don't repeat entity name in method names (`GetAsync` not `GetFileAsync`)
- Accept/return DTOs only, never entities
- ID not inside UpdateDto - pass separately
- Call `UpdateAsync` explicitly (don't assume change tracking)
- Don't call other app services in the same module
- Use base class properties (`Clock`, `CurrentUser`, `GuidGenerator`, `L`) instead of injecting these services
- **Distinguish a full update from a patch.** Overwriting `DirectoryId`/`Name`/`CellName` unconditionally on
  a rename wipes metadata the client never sent — the update path here was split into full-update vs patch
  for exactly this reason (see "protect file descriptor invariants" / the Update tests).

## DTO Naming Conventions

| Purpose | Convention | Example |
|---------|------------|---------|
| Query input | `Get{Entity}Input` | `GetFileListInput` |
| Create input | `Create{Entity}Input` | `CreateFileInput` |
| Single entity output | `{Entity}Dto` | `FileDescriptorDto`, `DirectoryDescriptorDto` |
| Tree/aggregate output | `{Entity}InfoDto` | `DirectoryDescriptorInfoDto` (carries `Children`) |

## DTO Location
- Define DTOs in `*.Application.Contracts` project
- This allows sharing with clients (generated proxies, `HttpApi.Client`, the Angular proxy)

## Validation

### Data Annotations
```csharp
public class CreateFileInput
{
    [Required]
    public string ContainerName { get; set; }

    [Required]
    public IRemoteStreamContent File { get; set; }
}
```

Decide whether a rule is a **domain rule** (put it in the entity/domain service — e.g. blob-name
uniqueness, directory-parent validity) or an **application rule** (DTO shape, input format) before reaching
for `IValidatableObject`/FluentValidation.

## Error Handling

```csharp
throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist).WithData("Name", name);

var file = await _fileDescriptorRepository.FindAsync(id);
if (file == null) throw new EntityNotFoundException(typeof(FileDescriptor), id);

throw new UserFriendlyException(L["SomeUserFacingMessage"]);
```

## Auto API Controllers
This module exposes its AppServices as ABP **conventional (auto) API controllers** — there are no
hand-written controllers. `FileExplorerHttpApiModule` registers the application part and tunes the
conventions (e.g. `options.ConventionalControllers.FormBodyBindingIgnoredTypes.Add(typeof(CreateFileInput))`
so multipart uploads bind correctly).
- Interface must inherit `IApplicationService` (which already has `[RemoteService]`)
- HTTP verb comes from the method-name prefix (Get, Create, Update, Delete)
- After changing an AppService signature, **regenerate the clients** — the C# proxies in
  `HttpApi.Client` and the Angular proxy under `angular/projects/file-explorer/src/lib/proxy` both drift
  otherwise (the audit found real proxy/contract drift). See `framework/common/cli-commands.md`.

---

## In this repo

### Object mapping runs on **Mapperly**, not AutoMapper
Mapping lives in `FileExplorerApplicationMappers.cs` (Application project). Each map is a compile-time
Mapperly partial extending ABP's `MapperBase<TSource, TDestination>`:

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FileDescriptorToDtoMapper : MapperBase<FileDescriptor, FileDescriptorDto>
{
    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]   // Url is resolved per-request, not stored
    public override partial FileDescriptorDto Map(FileDescriptor source);
    public override partial void Map(FileDescriptor source, FileDescriptorDto destination);
}
```

- Consume it through ABP's `IObjectMapper` (`ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(...)`),
  registered by `AbpMapperlyModule` (a dependency of `FileExplorerApplicationModule`).
- `RequiredMappingStrategy.Target` makes any unmapped destination property a **build error**, so every new
  DTO field is either mapped by name or explicitly `[MapperIgnoreTarget]`-ed.
- **Computed, non-stored fields are ignored and filled in the AppService**: `FileDescriptorDto.Url`
  (resolved from the blob provider per request) and `DirectoryDescriptorInfoDto.Children` (the directory
  tree, assembled by `DirectoryDescriptorAppService`).
- The repo was deliberately moved off AutoMapper ("map through Mapperly to drop the AutoMapper dependency")
  to keep the published packages clear of AutoMapper's advisory and match ABP's own modules. **Don't
  reintroduce AutoMapper** or a hand-written `MapToDto` switch — extend `FileExplorerApplicationMappers`.

### Authorization is resource-based, not just `[Authorize]`
`FileDescriptorAppService`/`DirectoryDescriptorAppService` authorize **individual resources** through
`IAuthorizationService` + `OperationAuthorizationRequirement` (Get/Create/Update/Delete), on top of the
coarse `FileExplorerPermissions.Files.Management` permission. Batch operations must authorize **each**
resource, not just check the management permission once — see `framework/common/authorization.md`.
