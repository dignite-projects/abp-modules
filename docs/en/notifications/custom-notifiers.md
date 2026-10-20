```json
//[doc-seo]
{
    "Description": "Write your own notification channel for the Dignite Notifications module, such as SMS, Web Push or a webhook, by implementing INotificationNotifier."
}
```

# Writing Your Own Notifier

A channel is a **notifier**: a class that implements `INotificationNotifier` and delivers one notification to one recipient. SMS, Web Push, a webhook or a chat integration are all written the same way, and none of them needs a change to the framework or to the notifications that already exist.

## Creating the Notifier

Put the notifier in a project of its own that depends on `Dignite.Abp.Notifications.Abstractions` **only** (plus the SDK of the service it calls). That keeps the channel independent of how notifications are distributed.

````csharp
public class SmsNotifier : INotificationNotifier, ITransientDependency
{
    public const string ChannelName = "Sms";

    private readonly INotificationDataSerializer _dataSerializer;
    private readonly ISmsSender _smsSender;
    private readonly IUserPhoneNumberFinder _phoneNumberFinder;

    public SmsNotifier(
        INotificationDataSerializer dataSerializer,
        ISmsSender smsSender,
        IUserPhoneNumberFinder phoneNumberFinder)
    {
        _dataSerializer = dataSerializer;
        _smsSender = smsSender;
        _phoneNumberFinder = phoneNumberFinder;
    }

    public string Name => ChannelName;

    public async Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default)
    {
        var phoneNumber = await _phoneNumberFinder.FindAsync(request.UserId, cancellationToken);
        if (phoneNumber == null)
        {
            return; // Nothing to deliver to: skip this recipient
        }

        var payload = NotificationPayload.FromRequest(request, _dataSerializer);
        var text = payload.Data is MessageNotificationData message
            ? message.Message
            : payload.NotificationName;

        await _smsSender.SendAsync(new SmsMessage(phoneNumber, text));
    }
}
````

Then register the channel in the notifier's module:

````csharp
[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class MyProjectNotificationsSmsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationNotifierOptions>(options =>
        {
            options.Notifiers.Add<SmsNotifier>(SmsNotifier.ChannelName);
        });
    }
}
````

`Name` is the channel name that [routes](routing.md) refer to, and it must equal the name the notifier is registered with. A channel maps to exactly one notifier type; two types claiming one channel fail the application start.

## The Delivery Contract

* `DeliverAsync` is called **once per recipient and channel**, with a `NotificationDeliveryRequestedEto` that carries the notification's id, name, payload, severity, creation time, entity, tenant and the recipient's `UserId`.
* It is called inside the notification's **tenant**: the event bus has already entered it, so do not change the tenant yourself.
* Delivery is **best-effort**. To skip a recipient, for example one without a phone number, simply return. An exception is logged and dropped; it is not retried. The inbox row, when the Notification Center is installed, is the authoritative record.
* Observe the `cancellationToken`.
* Only the notifier registered for the delivery's channel is created, so a notifier that fails to construct does not affect the other channels, and a process that does not host a channel ignores its deliveries.

## Reading the Payload

The event carries the payload **pre-serialized**, as the JSON with its type name (`request.DataJson`), so that it survives any transport: ABP's event bus, the outbox and inbox included, serializes events with plain `System.Text.Json`, without the application's JSON settings. Use `NotificationPayload.FromRequest(request, serializer)` with the injected `INotificationDataSerializer` to get the typed payload. The read is tolerant: an unknown or malformed payload becomes an `UnsupportedNotificationData` instead of throwing.

`NotificationPayload` is a view for rendering in the process. A notifier that sends the notification to an **external client** should not serialize it as it is; define a flat message type of its own that carries the payload as raw JSON, as the SignalR notifier does with `SignalRNotificationMessage`.

## See Also

* [Routing to Channels](routing.md)
* [Email Notifier](email.md), for an example of address and content provider chains
