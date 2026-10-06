using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationPushContentProvider_Tests
{
    [NotificationDataType("Test.PushPromo")]
    private sealed class PromoNotificationData : MessageNotificationData
    {
        public PromoNotificationData(string message) : base(message) { }
    }

    [NotificationDataType("Test.PushUnrelated")]
    private sealed class UnrelatedNotificationData : NotificationData
    {
    }

    private sealed class StaticProvider : INotificationPushContentProvider
    {
        private readonly string _body;

        public int Order { get; }

        public StaticProvider(int order, string body)
        {
            Order = order;
            _body = body;
        }

        public Task<NotificationPushContent?> BuildOrNullAsync(
            NotificationPushBuildContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<NotificationPushContent?>(new NotificationPushContent("title", _body));
        }
    }

    private sealed class NullLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => throw new NotSupportedException();

        public IStringLocalizer Create(string baseName, string location) => throw new NotSupportedException();
    }

    [Fact]
    public async Task The_message_fallback_handles_a_derived_payload_and_leaves_the_title_to_the_system()
    {
        var content = await new MessageNotificationPushContentProvider()
            .BuildOrNullAsync(Context(new PromoNotificationData("Sale!")));

        content.ShouldNotBeNull();
        content.Title.ShouldBeNull();
        content.Body.ShouldBe("Sale!");
    }

    [Fact]
    public async Task A_typed_provider_ignores_an_unrelated_payload()
    {
        var content = await new MessageNotificationPushContentProvider()
            .BuildOrNullAsync(Context(new UnrelatedNotificationData()));

        content.ShouldBeNull();
    }

    [Fact]
    public async Task The_message_fallback_skips_an_empty_message()
    {
        var content = await new MessageNotificationPushContentProvider()
            .BuildOrNullAsync(Context(new MessageNotificationData(" ")));

        content.ShouldBeNull();
    }

    [Fact]
    public async Task The_localizable_message_fallback_falls_back_to_the_key_without_a_localizer()
    {
        var data = new LocalizableMessageNotificationData(resourceName: null!, name: "OrderShipped")
        {
            Arguments = new Dictionary<string, object> { ["OrderNo"] = "A-42" }
        };

        var content = await new LocalizableMessageNotificationPushContentProvider(new NullLocalizerFactory())
            .BuildOrNullAsync(Context(data));

        content.ShouldNotBeNull();
        content.Body.ShouldBe("OrderShipped");
    }

    [Fact]
    public async Task The_default_builder_puts_business_providers_ahead_of_the_built_in_fallbacks()
    {
        var builder = new DefaultNotificationPushBuilder(new INotificationPushContentProvider[]
        {
            new MessageNotificationPushContentProvider(),
            new StaticProvider(NotificationPushProviderOrders.Default, "business")
        });

        var content = await builder.BuildAsync(Context(new MessageNotificationData("built-in")));

        content.ShouldNotBeNull();
        content.Body.ShouldBe("business");
    }

    [Fact]
    public async Task The_default_builder_returns_the_first_non_null_result_by_order()
    {
        var builder = new DefaultNotificationPushBuilder(new INotificationPushContentProvider[]
        {
            new StaticProvider(20, "later"),
            new StaticProvider(10, "earlier")
        });

        var content = await builder.BuildAsync(Context(new UnrelatedNotificationData()));

        content.ShouldNotBeNull();
        content.Body.ShouldBe("earlier");
    }

    private static NotificationPushBuildContext Context(NotificationData data)
    {
        return new NotificationPushBuildContext(
            new NotificationPayload
            {
                NotificationId = Guid.NewGuid(),
                NotificationName = "test",
                Data = data,
                Severity = NotificationSeverity.Info,
                CreationTime = DateTime.UtcNow
            },
            Guid.NewGuid(),
            null);
    }
}
