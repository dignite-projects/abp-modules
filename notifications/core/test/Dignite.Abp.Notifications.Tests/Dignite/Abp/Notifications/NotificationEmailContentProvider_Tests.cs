using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationEmailContentProvider_Tests
{
    /// <summary>A host's payload derived from a built-in one — the case the generic base must keep handling.</summary>
    [NotificationDataType("Test.Promo")]
    private sealed class PromoNotificationData : MessageNotificationData
    {
        public PromoNotificationData(string message) : base(message) { }
    }

    [NotificationDataType("Test.Unrelated")]
    private sealed class UnrelatedNotificationData : NotificationData
    {
    }

    private sealed class SpyProvider : NotificationEmailContentProvider<MessageNotificationData>
    {
        public MessageNotificationData? Narrowed { get; private set; }

        protected override Task<NotificationEmail?> BuildOrNullAsync(
            NotificationEmailBuildContext context,
            MessageNotificationData data,
            CancellationToken cancellationToken)
        {
            Narrowed = data;
            return Task.FromResult<NotificationEmail?>(new NotificationEmail("subject", data.Message));
        }
    }

    private static NotificationEmailBuildContext Context(NotificationData data)
    {
        return new NotificationEmailBuildContext(
            new NotificationPayload
            {
                NotificationId = Guid.NewGuid(),
                NotificationName = "test",
                Data = data,
                Severity = NotificationSeverity.Info,
                CreationTime = DateTime.UtcNow
            },
            Guid.NewGuid(),
            "a@b.c",
            null);
    }

    [Fact]
    public async Task A_typed_provider_still_handles_a_derived_payload()
    {
        // `is TData` honours subtyping. A generic INotificationEmailContentProvider<TData> resolved from DI by
        // Data.GetType() would look up the closed type exactly and silently stop handling this.
        var provider = new SpyProvider();

        var email = await provider.BuildOrNullAsync(Context(new PromoNotificationData("50% off")));

        email.ShouldNotBeNull();
        email!.Body.ShouldBe("50% off");
        provider.Narrowed.ShouldBeOfType<PromoNotificationData>();
    }

    [Fact]
    public async Task A_typed_provider_passes_on_an_unrelated_payload()
    {
        var provider = new SpyProvider();

        (await provider.BuildOrNullAsync(Context(new UnrelatedNotificationData()))).ShouldBeNull();

        // The guard ran before the implementer's code, so it never saw the wrong payload.
        provider.Narrowed.ShouldBeNull();
    }

    [Fact]
    public async Task Built_in_providers_narrow_their_own_payload_and_pass_on_others()
    {
        var message = new MessageNotificationEmailContentProvider();

        (await message.BuildOrNullAsync(Context(new MessageNotificationData("hi"))))!.Body.ShouldBe("hi");
        (await message.BuildOrNullAsync(Context(new UnrelatedNotificationData()))).ShouldBeNull();
    }

    [Fact]
    public void An_application_provider_outranks_the_built_in_fallbacks_by_default()
    {
        new SpyProvider().Order.ShouldBe(NotificationEmailProviderOrders.Default);
        new MessageNotificationEmailContentProvider().Order
            .ShouldBe(NotificationEmailProviderOrders.BuiltInFallback);

        NotificationEmailProviderOrders.Default
            .ShouldBeLessThan(NotificationEmailProviderOrders.BuiltInFallback);
    }
}
