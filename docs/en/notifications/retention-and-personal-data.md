```json
//[doc-seo]
{
    "Description": "How long the Dignite Notification Center keeps notifications, how to clean up old inbox rows safely, and how it erases a user's personal data on a GDPR request."
}
```

# Retention and Personal Data

The Notification Center **expires nothing by itself**. An inbox row lives until its user deletes it, and a notification payload lives until your application removes it, the way a phone's notification center keeps an entry until the user swipes it away. This page covers cleaning up old data, and erasing a user's data on request.

## Cleaning Up Old Notifications

Inboxes grow with the number of notifications times their recipients. When to delete, in which tenant, and which instance runs the cleanup in a cluster are decisions of your application, so the module does not schedule a cleanup on your behalf. Schedule one yourself, with an ABP [background worker](https://abp.io/docs/latest/framework/infrastructure/background-workers) or whatever already fits your operations, and follow two rules:

* **Never delete `Unread` rows.** Only old `Read` inbox rows are safe to remove. Deleting an unread row drops a notification silently and corrupts the unread count.
* **Delete a `Notification` payload only once no inbox row references it.** The payload is shared by the inbox rows of all its recipients.

A relational example, run for each tenant (`CurrentTenant.Change(tenantId)`, and once for the host), in bounded batches on a large database:

````sql
-- 1) Old READ inbox rows; Unread (State = 0) rows are always kept
DELETE FROM NotifUserNotifications
WHERE State = 1 AND CreationTime < @cutoff AND TenantId = @tenantId;

-- 2) Payloads that no inbox row references any more
DELETE FROM NotifNotifications
WHERE CreationTime < @cutoff AND TenantId = @tenantId
  AND NOT EXISTS (
      SELECT 1 FROM NotifUserNotifications u WHERE u.NotificationId = NotifNotifications.Id);
````

On MongoDB, collect the ids of unreferenced payloads first and delete them in a second step, instead of joining the collections.

| Record | Deleted by |
|---|---|
| `UserNotification` (inbox row) | Its user, through the inbox. Your cleanup may also delete old `Read` rows, never `Unread` ones. |
| `Notification` (payload) | Your cleanup, once it is older than your retention window **and** no inbox row references it. |
| `NotificationSubscription` | Its user, through the subscription API, by its exact scope. Not time-based. |
| `PushDevice` | The registry itself: unregistered devices, devices over the per-user limit and devices the provider reports as dead. |
| ABP event inbox and outbox records | ABP's own cleanup; see [Delivery Guarantees](delivery-guarantees.md#in-production). |

All of a user's inbox rows, subscriptions and devices are also deleted on a personal data erasure request.

## Personal Data Erasure

The Notification Center handles ABP's `GdprUserDataDeletionRequestedEto` event (from `Volo.Abp.Gdpr.Abstractions`) and erases what it keeps about that user: **every inbox row**, read and unread, **every subscription**, and **every push device**. Installing the Notification Center registers the handler; there is nothing to configure.

ABP's open-source packages only define the event, and nothing in this module publishes it. Your application needs a publisher: the [GDPR module](https://abp.io/docs/latest/modules/gdpr), or your own code through `IDistributedEventBus`. Without one, the handler never runs.

* **The shared payload stays.** A `Notification` has no owner, and the inbox rows of every recipient reference it, so it cannot be deleted for one user. Its payload may still contain personal data, such as a name in a message; the cleanup above removes it once no inbox row references it.
* **The tenant.** The handler deletes the user's rows by user id, in every tenant that shares the database; user ids are unique, so no other user's rows can match. When the publisher sets the event's `TenantId`, the event bus runs the handler in that tenant, which is what makes it reach the right database when each tenant has its own.
* **Retries.** Every delete is idempotent, so a redelivered event simply finishes the job. The deletes share one unit of work, which is atomic only where the unit of work is transactional (not on a standalone MongoDB server), so a failure part-way leaves the erasure partly done until the event is delivered again. ABP's event inbox retries a failed event; without an inbox, the exception goes back to the publisher.

To change what is erased, replace `GdprUserDataDeletionRequestedHandler` (its method is `virtual`), or add another `IDistributedEventHandler<GdprUserDataDeletionRequestedEto>` for your own data.

## See Also

* [Notification Center](notification-center.md)
* [Background Workers](https://abp.io/docs/latest/framework/infrastructure/background-workers)
