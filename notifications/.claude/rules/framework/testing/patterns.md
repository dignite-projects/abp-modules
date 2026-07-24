---
paths:
  - "test/**/*.cs"
  - "tests/**/*.cs"
  - "**/*Tests*/**/*.cs"
  - "**/*Test*.cs"
---

# Testing Conventions in This Repo

> **ABP testing docs**: https://abp.io/docs/latest/testing

Stack: **xUnit** + **Shouldly** + **NSubstitute** + `Volo.Abp.TestBase` (Autofac). EF Core
integration tests run against an in-memory **Sqlite** provider — no real database needed. The
MongoDB provider tests run against an **embedded mongod** started by MongoSandbox (bundled binary,
no local MongoDB install) — see "Cross-provider tests" below.

## Test naming: descriptive sentences, NOT `Should_X_When_Y`

The generic ABP template convention (`Should_Create_Book_When_Input_Is_Valid`) is **not** what
this repo actually uses. Match the existing style — a plain-English sentence, snake_cased,
describing the observed behavior:

```csharp
// ✅ Actual convention used in this repo
public async Task Round_trips_custom_notification_data_through_the_store()
public async Task Persisted_data_uses_the_discriminator_and_no_assembly_qualified_name()

// ❌ Don't introduce the generic ABP-template style here
public async Task Should_Create_Book_When_Input_Is_Valid()
```

Test class naming still follows `{TypeUnderTest}_Tests` (e.g. `NotificationStore_Tests`,
`NotificationAppService_Tests`) — that part matches the generic ABP convention.

## Base classes

Both `core/test` and `notification-center/test` follow the same shape: an abstract `*TestBase`
inheriting `AbpIntegratedTest<TTestModule>`, using Autofac. In `notification-center/test` the base
is **generic over the startup module** so the same tests run on multiple persistence providers
(see "Cross-provider tests" below):

```csharp
public abstract class NotificationCenterTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    // Helper used across this repo's tests — wrap direct store/repository calls in a UoW,
    // since (unlike an AppService call) resolving INotificationStore/IRepository directly
    // doesn't open one for you.
    protected virtual async Task WithUnitOfWorkAsync(Func<Task> func)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        await func();
        await uow.CompleteAsync();
    }

    // Helper for tests that need a specific authenticated user
    protected virtual IDisposable ChangeCurrentUser(Guid userId) { /* ... */ }
}
```

Concrete test classes inherit the relevant base and resolve what they need via
`GetRequiredService<T>()`. In `core/test` that base is the non-generic `DigniteAbpNotificationsTestBase`;
in `notification-center/test` it's `NotificationCenterTestBase<TStartupModule>`, closed over a
provider-specific module by the abstract scenario classes.

## Cross-provider tests (EF Core + MongoDB)

`NotificationStore` is provider-agnostic (it lives in `.Domain` and uses only the generic
`IRepository<T, Guid>`), so the store/app-service scenarios must pass identically on **both** EF
Core and MongoDB. `notification-center/test` is therefore three projects, not one:

| Project | Role |
|---|---|
| `Dignite.NotificationCenter.TestBase` | Shared, provider-independent. Holds `NotificationCenterTestBaseModule`, the generic `NotificationCenterTestBase<TStartupModule>`, test objects (`OrderShippedNotificationData`, `TestNotificationDefinitionProvider`, the embedded `HistoricalPayloadFixtures` used by tolerant-read tests), and the **abstract** scenario classes — `NotificationStore_Tests<T>`, `UserNotificationAppService_Tests<T>`, `NotificationDistribution_Tests<T>`, and `Notification_Outbox_Tests<T>`. Marked `<IsTestProject>false</IsTestProject>` — it has no runner, so `dotnet test` on the solution must not try to execute it. |
| `Dignite.NotificationCenter.EntityFrameworkCore.Tests` | EF Core / in-memory Sqlite provider. Thin subclasses of every abstract scenario, bound to `NotificationCenterEntityFrameworkCoreTestModule`. |
| `Dignite.NotificationCenter.MongoDB.Tests` | MongoDB provider. Thin subclasses of every abstract scenario, tagged `[Collection(MongoTestCollection.Name)]`. Most bind to `NotificationCenterMongoDbTestModule`; the outbox scenario binds to the separate `NotificationCenterMongoDbOutboxTestModule` and adds MongoDB-specific facts (both event boxes configured, ABP collection names + the unique `MessageId` index, concurrent inbox redelivery collapsing to one record). |

**Both providers run the transactional outbox contract.** `Notification_Outbox_Tests<T>` is a shared
scenario, not an EF-only one: MongoDB wires the outbox through `UseNotificationCenterMongoDbOutbox()`.
The two MongoDB modules differ deliberately — the regular one sets
`UnitOfWorkTransactionBehavior.Disabled` (single-document store/app-service tests don't need
transactions), while the outbox module sets `Enabled`, because the contract asserts that the
notification row and the outgoing event record commit or roll back together. It also turns off outbox
sending and inbox processing so the background workers can't drain the boxes mid-assertion.

**Adding a store/app-service scenario:** put the `[Fact]` on the abstract
`*_Tests<TStartupModule>` in `TestBase` so it runs on both providers automatically — don't add it
to only one provider project. Add a provider-specific concrete test only when the behavior is
genuinely provider-specific — for example MongoDB's unique `MessageId` inbox index and its
concurrent-redelivery semantics, which EF Core does not share.

**The MongoDB fixture** follows ABP v10's own pattern (mirrors `Volo.Abp.Identity.MongoDB.Tests`):
a static `MongoDbFixture` boots one embedded mongod for the session via `MongoRunner.Run(new
MongoRunnerOptions { UseSingleNodeReplicaSet = true })`; each run gets a random database name; test
classes share it through `[CollectionDefinition]`/`[Collection]`. Both Mongo test modules set the
connection string from the fixture; only the outbox module enables UoW transactions (see above).
Packages (`MongoSandbox.Core` + the OS-conditioned
`MongoSandbox8.runtime.*`) are pinned in `Directory.Packages.props`. MongoSandbox is the maintained
successor of EphemeralMongo — don't reintroduce `EphemeralMongo*` packages.

## Explicit `WithUnitOfWorkAsync` when testing stores/repositories directly

Most tests here exercise `INotificationStore` or `IRepository<T, Guid>` directly (not through an
AppService, which would open its own UoW). Wrap those calls:

```csharp
[Fact]
public async Task Round_trips_custom_notification_data_through_the_store()
{
    var notificationId = Guid.NewGuid();
    var userId = Guid.NewGuid();

    await WithUnitOfWorkAsync(async () =>
    {
        var store = GetRequiredService<INotificationStore>();
        await store.InsertNotificationAsync(new NotificationInfo { /* ... */ });
    });

    await WithUnitOfWorkAsync(async () =>
    {
        var store = GetRequiredService<INotificationStore>();
        var list = await store.GetUserNotificationsAsync(userId);

        list.Count.ShouldBe(1);
        var data = list.Single().Notification.Data.ShouldBeOfType<OrderShippedNotificationData>();
        data.OrderNumber.ShouldBe("SO-1001");
    });
}
```

## Assertions — Shouldly

```csharp
result.ShouldNotBeNull();
result.Name.ShouldBe("Expected");
entity.Data.ShouldNotContain("Version=");           // e.g. asserting no AssemblyQualifiedName leaked
var data = notification.Data.ShouldBeOfType<MyNotificationData>();

await Should.ThrowAsync<BusinessException>(async () => await _service.DoSomethingAsync());
```

## Mocking — NSubstitute

```csharp
var emailSender = Substitute.For<IEmailSender>();
emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
    .Returns(Task.CompletedTask);
context.Services.AddSingleton(emailSender);
```

## Custom `NotificationData` for tests

When a test needs a concrete `NotificationData` subclass, define a small test-only one (see
`OrderShippedNotificationData` in
`notification-center/test/Dignite.NotificationCenter.TestBase/OrderShippedNotificationData.cs`)
tagged with its own `[NotificationDataType("Test.OrderShipped")]` — don't reuse a production type
just because it's convenient, and don't skip the discriminator (that's exactly what
`notifications-invariants.md` §1 tests are meant to catch).

## General best practices (still apply)

- Each test independent; don't share mutable state between tests.
- Test edge cases and error conditions, not just the happy path.
- Prefer integration tests with real services over mocking internals — mock only true externals
  (email/SMS gateways, etc.).
