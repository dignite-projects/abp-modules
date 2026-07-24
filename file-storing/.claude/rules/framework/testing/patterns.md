---
paths:
  - "test/**/*.cs"
  - "tests/**/*.cs"
  - "**/*Tests*/**/*.cs"
  - "**/*Test*.cs"
---

# Testing Conventions in This Repo

> **ABP testing docs**: https://abp.io/docs/latest/testing

Stack: **xUnit** + **Shouldly** + **NSubstitute** + `Volo.Abp.TestBase` (Autofac). EF Core integration tests
run against an in-memory **Sqlite** provider — no real database needed. The MongoDB provider tests run against
an **embedded mongod** started by MongoSandbox (bundled binary, no local MongoDB install) — see "Cross-provider
tests" below.

## Test naming: method-anchored `Method_ShouldX[_WhenY]`

This repo's convention is a **method-anchored Should sentence** — the method under test, then the expected
behavior, optionally a condition. Match what's here rather than the generic ABP-template `Should_X_When_Y`
style.

```csharp
// ✅ Actual convention used in this repo
public async Task BlobNameExistsAsync_ShouldBeTenantScoped()
public async Task CreateAsync_ShouldRejectParentFromAnotherOwnerOrContainer()
public async Task MoveAsync_ShouldRejectMovingDirectoryIntoItself()
public async Task Rename_ShouldPreserveDirectoryAndCellName()
public async Task FileSizeLimitHandler_Should_Reject_Too_Large_Stream()      // underscores also seen in core
public async Task ExecuteAsync_Should_Resize_Image_Larger_Than_Preset()
```

Test class naming is `{TypeUnderTest}_Tests` (e.g. `FileDescriptorManager_Tests`, `FileDescriptorAppService_Tests`,
`ImageResizeHandler_Tests`, `FileDescriptorRepository_Tests`).

## Base classes

Everything descends from a generic base in `Dignite.FileExplorer.TestBase`:

```csharp
public abstract class FileExplorerTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
        => options.UseAutofac();
}
```

Each test project closes that generic over its own startup module, so the same helpers work everywhere:

| Base | Startup module | Project |
|---|---|---|
| `FileExplorerDomainTestBase` | `FileExplorerDomainTestModule` | `Domain.Tests` |
| `FileExplorerApplicationTestBase` | `FileExplorerApplicationTestModule` | `Application.Tests` |
| `FileExplorerEntityFrameworkCoreTestBase` | `FileExplorerEntityFrameworkCoreTestModule` | `EntityFrameworkCore.Tests` |
| `FileExplorerMongoDbTestBase` | `FileExplorerMongoDbTestModule` | `MongoDB.Tests` |

Concrete test classes inherit the relevant base and resolve what they need via `GetRequiredService<T>()`.
When resolving a repository or manager directly (not through an AppService), wrap the calls in a unit of work.

## Cross-provider tests (EF Core + MongoDB)

The custom repositories (`IFileDescriptorRepository`, `IDirectoryDescriptorRepository`) have both an EF Core and
a MongoDB implementation, so their contracts must pass identically on both. Put those scenarios as **abstract
generic classes in `TestBase`** and inherit them from each provider project — the repo literally documents this
in `FileDescriptorRepository_Tests`:

```csharp
// In Dignite.FileExplorer.TestBase — provider-independent:
public abstract class FileDescriptorRepository_Tests<TStartupModule> : FileExplorerTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact] public async Task BlobNameExistsAsync_ShouldBeTenantScoped() { /* ... */ }
}

// In EntityFrameworkCore.Tests and MongoDB.Tests — thin concrete subclasses bound to the provider module.
```

**Adding a repository/store scenario:** put the `[Fact]` on the abstract `*_Tests<TStartupModule>` in `TestBase`
so it runs on both providers automatically. Add a provider-specific concrete test only when the behavior is
genuinely provider-specific.

## The specialized test projects (what each guards)

Beyond the two provider projects, the suite is split by concern — several of these projects exist because a
specific hardening fix needed a home:

| Project | Guards |
|---|---|
| `Domain.Tests` | Domain managers — `FileDescriptorManager`, `DirectoryManager` create/move/validate rules |
| `Application.Tests` | AppService behavior, container authorization configuration, mapping |
| `Authorization.Tests` | Resource-based authorization & permission gating (`Create_ShouldBeDeniedWithoutCreatePermission`, `Delete_ShouldBeDeniedWithoutDeletePermissionForNonOwner`, `Get_ShouldBeDeniedWithoutGetPermission`) |
| `DirectorySafety.Tests` | Directory cycle prevention (`MoveAsync_ShouldRejectMovingDirectoryInto{Itself,Descendant}`), non-empty deletion, and localization |
| `Update.Tests` | Rename/patch semantics don't wipe metadata (`Rename_ShouldPreserveDirectoryAndCellName`) |
| `core/test/…FileStoring.Tests` | `FileSizeLimitHandler`, `FileTypeCheckHandler`, `ContainerNameValidator` |
| `core/test/…FileStoring.Imaging.Tests` | `ImageResizeHandler` (`AbpIntegratedTest<ImagingTestModule>`) |

When you fix a security/integrity bug, add its regression test to the matching project (or the shared abstract
scenario), following the fixes that already live there.

## The MongoDB fixture

Follows ABP v10's own pattern: a static fixture boots one embedded mongod for the session via MongoSandbox
(`MongoRunner.Run(...)`), each run gets a random database name, and test classes share it through
`[CollectionDefinition]`/`[Collection]`. Packages (`MongoSandbox.Core` + the OS-conditioned
`MongoSandbox8.runtime.*`) are pinned in `Directory.Packages.props`. MongoSandbox is the maintained successor of
EphemeralMongo — don't reintroduce `EphemeralMongo*`.

## Assertions — Shouldly

```csharp
result.ShouldNotBeNull();
result.Name.ShouldBe("Expected");
exists.ShouldBeTrue();
await Should.ThrowAsync<BusinessException>(async () => await _manager.MoveAsync(dir, dir.Id));
```

## Mocking — NSubstitute

Mock only true externals. For blob storage, prefer ABP's in-memory/file-system blob provider in the test module
over mocking `IBlobContainer`; mock things like an out-of-process image or virus-scan gateway if one is added.

```csharp
var probe = Substitute.For<ISomeExternalGateway>();
probe.CheckAsync(Arg.Any<Stream>()).Returns(Task.CompletedTask);
context.Services.AddSingleton(probe);
```

## General best practices (still apply)

- Each test independent; don't share mutable state between tests.
- Test edge cases and error conditions (oversized uploads, forged MIME, directory cycles, cross-tenant access),
  not just the happy path — that's what most of these projects are for.
- Prefer integration tests with real services over mocking internals.
