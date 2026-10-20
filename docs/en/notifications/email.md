```json
//[doc-seo]
{
    "Description": "Send notifications by email with the email notifier of the Dignite Notifications module: address resolvers, content providers and the recipient's culture."
}
```

# Email Notifier

The email notifier sends one email per recipient through ABP's [`IEmailSender`](https://abp.io/docs/latest/framework/infrastructure/emailing). It decides two things for every recipient, each with a chain of providers you can extend: **the address** to send to, and **the content** of the email.

## Installation

Install the `Dignite.Abp.Notifications.Emailing` package, and usually `Dignite.Abp.Notifications.Emailing.Identity` too, into the project that delivers the notifications, and depend on their modules:

````csharp
[DependsOn(
    typeof(AbpNotificationsEmailingModule),
    typeof(AbpNotificationsEmailingIdentityModule)
)]
public class MyProjectModule : AbpModule
{
}
````

Then [route](routing.md) notifications to the `Email` channel (`EmailNotifier.ChannelName`). The SMTP settings, or any other `IEmailSender` configuration, stay the application's; see [Emailing](https://abp.io/docs/latest/framework/infrastructure/emailing).

> The Emailing package alone resolves **no address**, so nothing is sent (and a warning is logged) until an address resolver exists. `Dignite.Abp.Notifications.Emailing.Identity` adds one that uses the email address of the recipient's ABP Identity account.

## Email Addresses

The address of a recipient comes from an ordered chain of `IEmailNotificationAddressResolver` services. The chain is ordered by `Order`, and the first resolver that returns an address wins. The Identity resolver runs at `NotificationEmailProviderOrders.BuiltInFallback` (1000), so a resolver of your own at `NotificationEmailProviderOrders.Default` (0) runs first and can claim the notifications it knows about.

**Example: Send an order notification to the contact address recorded on the order**

````csharp
public class OrderEmailNotificationAddressResolver : IEmailNotificationAddressResolver, ITransientDependency
{
    private readonly IOrderRepository _orderRepository;

    public OrderEmailNotificationAddressResolver(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public int Order => NotificationEmailProviderOrders.Default;

    public async Task<EmailNotificationAddress?> GetEmailOrNullAsync(
        EmailNotificationAddressResolveContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Notification.NotificationName != "MyProject.OrderShipped")
        {
            return null; // Not mine: fall through to the next resolver
        }

        var contact = await _orderRepository.FindContactAsync(
            context.Notification.EntityId!, context.UserId, cancellationToken);

        return contact == null
            ? null
            : EmailNotificationAddress.To(contact.Email, contact.CultureName);
    }
}
````

* A resolver returns the address **of this user** in the context of the notification, never "the address of the entity": the notifier builds the email for that user and sends one email per recipient.
* `EmailNotificationAddressResolveContext` provides the `Notification` (name, payload, entity), the `UserId` and the `TenantId`.
* Do not call `CurrentTenant.Change` in a resolver: the event bus has already entered the notification's tenant. `TenantId` is there for a resolver that must pass the tenant on to a remote service.

## Email Content

The content comes from a second chain, of `INotificationEmailContentProvider` services, ordered in the same way. Derive from `NotificationEmailContentProvider<TData>`, and the payload is narrowed to your type for you:

````csharp
public class OrderShippedEmailContentProvider
    : NotificationEmailContentProvider<OrderShippedNotificationData>, ITransientDependency
{
    private readonly IStringLocalizer<MyProjectResource> _localizer;

    public OrderShippedEmailContentProvider(IStringLocalizer<MyProjectResource> localizer)
    {
        _localizer = localizer;
    }

    protected override Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        OrderShippedNotificationData data,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<NotificationEmail?>(new NotificationEmail(
            _localizer["Email:OrderShipped:Subject", data.OrderNumber],
            _localizer["Email:OrderShipped:Body", data.OrderNumber, data.ItemCount],
            isBodyHtml: true));
    }
}
````

A provider typed on a payload class also handles the classes derived from it. Implement `INotificationEmailContentProvider` directly only for a provider that handles unrelated payload types; then check the payload type yourself, or the provider claims every notification.

When no provider returns content, no email is sent. The built-in providers, at `BuiltInFallback`, handle `MessageNotificationData` (the message as the body) and `LocalizableMessageNotificationData` (the localized message); they use the notification name as the subject, so write a provider of your own for any email a user will actually read. To render the body from a template, use ABP's [text templating](https://abp.io/docs/latest/framework/infrastructure/text-templating) in your provider.

## The Recipient's Culture

The content is built in the culture of the recipient, so the localization in a content provider follows the recipient's language:

* An address resolver can return a culture with the address (`EmailNotificationAddress.To(address, cultureName)`).
* The Identity resolver uses the user's language setting when [Setting Management](https://abp.io/docs/latest/modules/setting-management) is installed.
* Otherwise, `NotificationEmailOptions.DefaultCulture` (`en` by default) applies.

````csharp
Configure<NotificationEmailOptions>(options =>
{
    options.DefaultCulture = "ja";
});
````

The culture is set only while one recipient's email is built, and restored before the next. `NotificationEmailBuildContext.CultureName` tells a content provider which culture it is building for.

## Customizing the Notifier

To change how the notifier works, derive from `EmailNotifier` and replace it in [dependency injection](https://abp.io/docs/latest/framework/fundamentals/dependency-injection#replace-a-service), with `[Dependency(ReplaceServices = true)]` and `[ExposeServices(typeof(EmailNotifier))]`. Do not register a second notifier for the `Email` channel: a channel maps to one notifier type, and two fail the application start.

## See Also

* [Routing to Channels](routing.md)
* [Emailing](https://abp.io/docs/latest/framework/infrastructure/emailing)
