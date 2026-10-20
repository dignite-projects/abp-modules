```json
//[doc-seo]
{
    "Description": "What the Dignite Notification Center guarantees when it stores and delivers notifications, and how to make inbox rows and deliveries commit together with ABP's transactional outbox."
}
```

# Delivery Guarantees

Distributing a notification does two things for each batch of recipients: it writes their **inbox rows**, and it publishes one **`NotificationDeliveryRequestedEto`** per recipient and channel. This page explains what holds when something fails between the two, and how to make them commit together.

## Best-Effort Delivery

Channel delivery is **best-effort** by design. There is no per-recipient delivery record, lease or retry: a notifier that fails is logged, and the delivery is dropped. The **inbox row is the authoritative record**, which is why the UIs always refresh from the REST API and treat a real-time message only as a prompt.

## Making Inbox Rows and Deliveries Atomic

The inbox rows and the delivery events commit together only when the application uses ABP's [transactional outbox](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) **and** a transactional [unit of work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work):

| Setup | Inbox rows and deliveries atomic | After a failure part-way |
|---|---|---|
| EF Core, outbox, transactional unit of work | Yes, within one distribution | The transaction rolls back the inbox rows and the outbox records. |
| EF Core, no outbox, transactional unit of work | No | The inbox rows roll back, but an event already published may have escaped. |
| EF Core, non-transactional unit of work | No | Completed batches of inbox rows and their events remain. |
| MongoDB, outbox, transactional unit of work on a replica set | Yes, within one distribution | The transaction rolls back the inbox rows and the outbox records. |
| MongoDB, no outbox or non-transactional unit of work | No | Completed batches of inbox rows and their events remain. |
| No Notification Center | No inbox | A delivery event is sent once; nothing survives the process. |

Without the outbox, a crash between writing the inbox rows and publishing the events can leave a notification in the inbox that no channel delivered. Cancellation stops new work; it does not undo completed work. No setup guarantees exactly-once external delivery, or atomicity across the separate background jobs of one large notification.

### Entity Framework Core

Enable the outbox, and the event inbox, on the module's `DbContext` with one line in the `ConfigureServices` method of your module:

````csharp
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.UseNotificationCenterEfCoreOutbox();
});
````

`NotificationCenterDbContext` maps ABP's event box tables, so add them to your migration `DbContext` next to the Notification Center's tables, if it does not have them yet, and add a migration:

````csharp
builder.ConfigureEventInbox();
builder.ConfigureEventOutbox();
builder.ConfigureNotificationCenter();
````

### MongoDB

````csharp
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.UseNotificationCenterMongoDbOutbox();
});
````

The MongoDB outbox needs a **transaction-capable deployment**: a replica set running MongoDB 4.0 or later. A standalone server cannot commit the outbox transaction. The provider creates ABP's `AbpEventOutbox` and `AbpEventInbox` collections with indexes for the outbox sender and the inbox processor, and makes the inbox's `MessageId` unique, so two concurrent deliveries of one event cannot both be processed.

A MongoDB context of your own that implements `INotificationCenterMongoDbContext` must also implement ABP's `IHasEventInbox` and `IHasEventOutbox`, call `ConfigureEventInbox()` and `ConfigureEventOutbox()` in `CreateModel`, and configure both boxes on itself instead of calling `UseNotificationCenterMongoDbOutbox()`:

````csharp
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.Outboxes.Configure(config => config.UseMongoDbContext<MyMongoDbContext>());
    options.Inboxes.Configure(config => config.UseMongoDbContext<MyMongoDbContext>());
});
````

### In Production

* Keep the distribution in **transactional** units of work: do not set `AbpUnitOfWorkDefaultOptions.TransactionBehavior` to `Disabled`.
* With more than one instance of the application, configure a [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider for the outbox sender and the inbox processor.
* Processed inbox records are kept for ABP's deduplication window and then removed by ABP's cleanup worker. Tune `AbpEventBusBoxesOptions.WaitTimeToDeleteProcessedInboxEvents` and `CleanOldEventTimeIntervalSpan` for your redelivery window. Do not add TTL indexes or deletes that bypass ABP's status-aware cleanup.

## See Also

* [Distributed Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed)
* [Split Deployment](split-deployment.md)
