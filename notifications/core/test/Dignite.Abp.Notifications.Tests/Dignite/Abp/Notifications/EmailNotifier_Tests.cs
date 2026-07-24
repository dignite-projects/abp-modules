using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace Dignite.Abp.Notifications;

public class EmailNotifier_Tests
{
    private static readonly NotificationDataSerializer DataSerializer =
        NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));

    [Fact]
    public void Email_notifier_exposes_the_canonical_channel_contract()
    {
        var notifier = new EmailNotifier(
            Substitute.For<IEmailSender>(),
            new[] { Substitute.For<IEmailNotificationAddressResolver>() },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        notifier.Name.ShouldBe(EmailNotifier.ChannelName);
        notifier.ShouldBeAssignableTo<INotificationNotifier>();

        var exposedServices = typeof(EmailNotifier)
            .GetCustomAttribute<ExposeServicesAttribute>()!
            .ServiceTypes;
        exposedServices.Count(type => type == typeof(INotificationNotifier)).ShouldBe(1);
        exposedServices.ShouldBe(new[] { typeof(INotificationNotifier), typeof(EmailNotifier) });
    }

    [Fact]
    public async Task Emails_only_users_with_a_resolved_address()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var userWithEmail = Guid.NewGuid();
        var userWithoutEmail = Guid.NewGuid();
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == userWithEmail)).Returns(EmailNotificationAddress.To("a@b.com"));
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == userWithoutEmail)).Returns((EmailNotificationAddress?)null);

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        await DeliverAsync(notifier, userWithEmail, userWithoutEmail);

        await emailSender.Received(1).SendAsync(
            Arg.Is<string>(to => to == "a@b.com"),
            Arg.Is<string>(subject => subject == "order.shipped"),
            Arg.Is<string>(body => body == "Shipped!"),
            Arg.Any<bool>());
        // Exactly one email total — the user without an address got nothing.
        await emailSender.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Sends_one_email_per_recipient_even_when_their_addresses_collide()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        // Two users, one mailbox. ASP.NET Core Identity's UserOptions.RequireUniqueEmail defaults to false, so a
        // shared team or family account is a legitimate configuration, not a misconfiguration.
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("shared@example.com"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        await DeliverAsync(notifier, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Deliberately NOT deduplicated by address. The body is built per recipient
        // (NotificationEmailBuildContext carries the UserId), so collapsing three recipients into one send would
        // silently drop two personalized emails. A provider returning one address for every user is violating its
        // contract; N loud duplicates point straight at it, whereas a silently dropped email does not.
        await emailSender.Received(3).SendAsync(
            "shared@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Rejects_a_request_for_another_channel()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("a@b.com"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        await Should.ThrowAsync<InvalidOperationException>(
            () => notifier.DeliverAsync(CreateRequest(Guid.NewGuid(), channel: "SignalR")));

        await emailSender.DidNotReceiveWithAnyArgs().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Skips_when_no_provider_can_build_email_content()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("a@b.com"));
        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await notifier.DeliverAsync(CreateRequest(Guid.NewGuid(), new OrderShippedNotificationData()));

        await emailSender.DidNotReceiveWithAnyArgs().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Builds_email_content_per_resolved_recipient()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var builder = new CapturingEmailBuilder();
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == firstUserId)).Returns(EmailNotificationAddress.To("first@example.com"));
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == secondUserId)).Returns(EmailNotificationAddress.To("second@example.com"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            builder,
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        var tenantId = Guid.NewGuid();
        await notifier.DeliverAsync(CreateRequest(firstUserId, tenantId: tenantId));
        await notifier.DeliverAsync(CreateRequest(secondUserId, tenantId: tenantId));

        builder.Contexts.Select(context => context.UserId).ShouldBe(new[] { firstUserId, secondUserId });
        builder.Contexts.Select(context => context.EmailAddress).ShouldBe(new[] { "first@example.com", "second@example.com" });
        builder.Contexts.ShouldAllBe(context => context.TenantId == tenantId);
    }

    [Fact]
    public async Task Builds_email_content_under_each_recipient_culture_and_restores_the_previous_culture()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var builder = new CapturingEmailBuilder();
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == firstUserId))
            .Returns(EmailNotificationAddress.To("first@example.com", "fr-FR"));
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == secondUserId))
            .Returns(EmailNotificationAddress.To("second@example.com", "ja-JP"));

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            builder,
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await DeliverAsync(notifier, firstUserId, secondUserId);

        builder.Cultures.ShouldBe(new[] { "fr-FR", "ja-JP" });
        builder.Contexts.Select(context => context.CultureName).ShouldBe(new[] { "fr-FR", "ja-JP" });
        CultureInfo.CurrentCulture.ShouldBe(previousCulture);
        CultureInfo.CurrentUICulture.ShouldBe(previousUICulture);
    }

    [Fact]
    public async Task Uses_the_configured_default_culture_when_a_resolver_does_not_supply_one()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var builder = new CapturingEmailBuilder();
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("default@example.com"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            builder,
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions { DefaultCulture = "en-US" }));

        await DeliverAsync(notifier, Guid.NewGuid());

        builder.Cultures.ShouldBe(new[] { "en-US" });
        builder.Contexts.Single().CultureName.ShouldBe("en-US");
    }

    [Fact]
    public async Task Keeps_emailing_the_remaining_recipients_when_one_recipient_culture_name_is_invalid()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var builder = new CapturingEmailBuilder();
        var brokenUserId = Guid.NewGuid();
        var laterUserId = Guid.NewGuid();
        // Must be invalid on every platform. Windows rejects "english"; ICU on Linux accepts it as a language subtag
        // and hands back a culture literally named "english" — see the silent-degradation note in #39. Only names with
        // characters BCP-47 forbids, like the space and "!" here, throw everywhere.
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == brokenUserId))
            .Returns(EmailNotificationAddress.To("broken@example.com", "not a culture!"));
        resolver.GetEmailOrNullAsync(Arg.Is<EmailNotificationAddressResolveContext>(
            context => context.UserId == laterUserId))
            .Returns(EmailNotificationAddress.To("later@example.com", "ja-JP"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            builder,
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions { DefaultCulture = "en-US" }));

        await DeliverAsync(notifier, brokenUserId, laterUserId);

        builder.Cultures.ShouldBe(new[] { "en-US", "ja-JP" });
        await emailSender.Received(2).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Falls_back_to_the_ambient_culture_when_the_configured_default_culture_is_invalid()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var builder = new CapturingEmailBuilder();
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("ambient@example.com"));

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            builder,
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions { DefaultCulture = "not a culture!" }));

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            // Stand in for invariant globalization, where the ambient culture is the invariant one and no culture name
            // resolves at all. Its Name is the empty string, which the build context must not reject as blank.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            await DeliverAsync(notifier, Guid.NewGuid());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }

        builder.Cultures.ShouldBe(new[] { string.Empty });
        builder.Contexts.Single().CultureName.ShouldBe(string.Empty);
        await emailSender.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Restores_the_previous_culture_when_content_building_fails()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>())
            .Returns(EmailNotificationAddress.To("failure@example.com", "de-DE"));
        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            new ThrowingEmailBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        await Should.ThrowAsync<InvalidOperationException>(
            () => notifier.DeliverAsync(CreateRequest(Guid.NewGuid())));

        CultureInfo.CurrentCulture.ShouldBe(previousCulture);
        CultureInfo.CurrentUICulture.ShouldBe(previousUICulture);
    }

    [Fact]
    public async Task Passes_event_tenant_to_the_address_resolver()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        EmailNotificationAddressResolveContext? capturedContext = null;

        resolver.GetEmailOrNullAsync(Arg.Any<EmailNotificationAddressResolveContext>()).Returns(call =>
        {
            capturedContext = call.Arg<EmailNotificationAddressResolveContext>();
            return Task.FromResult<EmailNotificationAddress?>(EmailNotificationAddress.To("tenant@example.com"));
        });

        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));
        await notifier.DeliverAsync(CreateRequest(userId, tenantId: tenantId));

        capturedContext.ShouldNotBeNull();
        capturedContext!.UserId.ShouldBe(userId);
        capturedContext.TenantId.ShouldBe(tenantId);
        capturedContext.Notification.NotificationName.ShouldBe("order.shipped");
    }

    [Fact]
    public async Task Default_builder_uses_provider_order_and_allows_business_providers_before_built_in_fallbacks()
    {
        var businessProvider = new StaticEmailContentProvider(
            NotificationEmailProviderOrders.Default,
            _ => new NotificationEmail("business", "custom"));
        var builder = new DefaultNotificationEmailBuilder(new INotificationEmailContentProvider[]
        {
            new MessageNotificationEmailContentProvider(),
            businessProvider
        });

        var email = await builder.BuildAsync(CreateContext(new MessageNotificationData("built-in")));

        email.ShouldNotBeNull();
        email.Subject.ShouldBe("business");
        email.Body.ShouldBe("custom");
    }

    [Fact]
    public async Task Default_builder_returns_first_non_null_provider_by_order()
    {
        var laterProvider = new StaticEmailContentProvider(
            20,
            _ => new NotificationEmail("later", "later"));
        var earlierProvider = new StaticEmailContentProvider(
            10,
            _ => new NotificationEmail("earlier", "earlier"));
        var builder = new DefaultNotificationEmailBuilder(new INotificationEmailContentProvider[]
        {
            laterProvider,
            earlierProvider
        });

        var email = await builder.BuildAsync(CreateContext(new OrderShippedNotificationData()));

        email.ShouldNotBeNull();
        email.Subject.ShouldBe("earlier");
    }

    [Fact]
    public async Task Default_builder_supports_localizable_message_notification_data()
    {
        var builder = new DefaultNotificationEmailBuilder(new INotificationEmailContentProvider[]
        {
            new LocalizableMessageNotificationEmailContentProvider(new TestStringLocalizerFactory())
        });
        var data = new LocalizableMessageNotificationData(resourceName: null!, name: "OrderShipped")
        {
            Arguments = new Dictionary<string, object> { ["OrderNo"] = "A-42" }
        };

        var email = await builder.BuildAsync(CreateContext(data));

        email.ShouldNotBeNull();
        email.Subject.ShouldBe("test.notification");
        email.Body.ShouldBe("OrderShipped");
    }

    // ---- the address resolver chain, which EmailNotifier now owns rather than a separate aggregate service ----

    [Fact]
    public async Task Lower_order_resolver_wins_and_later_ones_are_not_consulted()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var business = new StubAddressResolver(NotificationEmailProviderOrders.Default, "order@example.com");
        var fallback = new StubAddressResolver(NotificationEmailProviderOrders.BuiltInFallback, "account@example.com");

        // Registration order deliberately reversed: Order, not DI order, decides.
        var notifier = new EmailNotifier(
            emailSender,
            new IEmailNotificationAddressResolver[] { fallback, business },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await DeliverAsync(notifier, Guid.NewGuid());

        await emailSender.Received(1).SendAsync(
            "order@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        business.Calls.ShouldBe(1);
        fallback.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_null_address_passes_to_the_next_resolver()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var passes = new StubAddressResolver(NotificationEmailProviderOrders.Default, null);
        var fallback = new StubAddressResolver(NotificationEmailProviderOrders.BuiltInFallback, "account@example.com");

        var notifier = new EmailNotifier(
            emailSender,
            new IEmailNotificationAddressResolver[] { passes, fallback },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await DeliverAsync(notifier, Guid.NewGuid());

        await emailSender.Received(1).SendAsync(
            "account@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        passes.Calls.ShouldBe(1);
        fallback.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task An_empty_resolver_chain_sends_nothing()
    {
        var emailSender = Substitute.For<IEmailSender>();

        var notifier = new EmailNotifier(
            emailSender,
            Array.Empty<IEmailNotificationAddressResolver>(),
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await DeliverAsync(notifier, Guid.NewGuid());

        await emailSender.DidNotReceiveWithAnyArgs().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Resolvers_of_equal_order_are_broken_by_type_name_so_the_chain_is_deterministic()
    {
        // Which mailbox a user receives mail at must not depend on module load order.
        foreach (var chain in new[]
                 {
                     new IEmailNotificationAddressResolver[] { new AaaAddressResolver(), new ZzzAddressResolver() },
                     new IEmailNotificationAddressResolver[] { new ZzzAddressResolver(), new AaaAddressResolver() }
                 })
        {
            var emailSender = Substitute.For<IEmailSender>();
            var notifier = new EmailNotifier(
                emailSender,
                chain,
                CreateDefaultBuilder(),
                DataSerializer,
                NullLogger<EmailNotifier>.Instance,
                Options.Create(new NotificationEmailOptions()));

            await DeliverAsync(notifier, Guid.NewGuid());

            await emailSender.Received(1).SendAsync(
                "aaa@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        }
    }

    private static async Task DeliverAsync(EmailNotifier notifier, params Guid[] userIds)
    {
        foreach (var userId in userIds)
        {
            await notifier.DeliverAsync(CreateRequest(userId));
        }
    }

    [Fact]
    public async Task Cancellation_reaches_the_address_resolution_boundary_and_stops_before_sending()
    {
        var emailSender = Substitute.For<IEmailSender>();
        var resolver = Substitute.For<IEmailNotificationAddressResolver>();
        using var cancellation = new CancellationTokenSource();
        CancellationToken receivedToken = default;
        resolver.GetEmailOrNullAsync(
                Arg.Any<EmailNotificationAddressResolveContext>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                receivedToken = call.ArgAt<CancellationToken>(1);
                cancellation.Cancel();
                receivedToken.ThrowIfCancellationRequested();
                return EmailNotificationAddress.To("never@example.com");
            });
        var notifier = new EmailNotifier(
            emailSender,
            new[] { resolver },
            CreateDefaultBuilder(),
            DataSerializer,
            NullLogger<EmailNotifier>.Instance,
            Options.Create(new NotificationEmailOptions()));

        await Should.ThrowAsync<OperationCanceledException>(
            () => notifier.DeliverAsync(CreateRequest(Guid.NewGuid()), cancellation.Token));

        receivedToken.ShouldBe(cancellation.Token);
        await emailSender.DidNotReceiveWithAnyArgs().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    private static NotificationDeliveryRequestedEto CreateRequest(
        Guid userId,
        NotificationData? data = null,
        Guid? tenantId = null,
        string channel = EmailNotifier.ChannelName)
    {
        var notificationId = Guid.NewGuid();
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = notificationId,
            NotificationName = "order.shipped",
            DataJson = DataSerializer.Serialize(data ?? new MessageNotificationData("Shipped!")),
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserId = userId,
            Channel = channel,
            TenantId = tenantId
        };
    }

    private class StubAddressResolver : IEmailNotificationAddressResolver
    {
        private readonly EmailNotificationAddress? _address;

        public int Order { get; }

        public int Calls { get; private set; }

        public StubAddressResolver(int order, string? address)
        {
            Order = order;
            _address = address == null ? null : EmailNotificationAddress.To(address);
        }

        public Task<EmailNotificationAddress?> GetEmailOrNullAsync(
            EmailNotificationAddressResolveContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_address);
        }
    }

    // Two distinct types at the same Order: "AaaAddressResolver" sorts before "ZzzAddressResolver" by FullName.
    private class AaaAddressResolver : IEmailNotificationAddressResolver
    {
        public int Order => NotificationEmailProviderOrders.Default;

        public Task<EmailNotificationAddress?> GetEmailOrNullAsync(
            EmailNotificationAddressResolveContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<EmailNotificationAddress?>(EmailNotificationAddress.To("aaa@example.com"));
    }

    private class ZzzAddressResolver : IEmailNotificationAddressResolver
    {
        public int Order => NotificationEmailProviderOrders.Default;

        public Task<EmailNotificationAddress?> GetEmailOrNullAsync(
            EmailNotificationAddressResolveContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<EmailNotificationAddress?>(EmailNotificationAddress.To("zzz@example.com"));
    }

    private static DefaultNotificationEmailBuilder CreateDefaultBuilder()
    {
        return new DefaultNotificationEmailBuilder(new INotificationEmailContentProvider[]
        {
            new MessageNotificationEmailContentProvider(),
            new LocalizableMessageNotificationEmailContentProvider(new TestStringLocalizerFactory())
        });
    }

    private static NotificationEmailBuildContext CreateContext(NotificationData data)
    {
        return new NotificationEmailBuildContext(
            new NotificationPayload
            {
                NotificationId = Guid.NewGuid(),
                NotificationName = "test.notification",
                Data = data,
                Severity = NotificationSeverity.Info,
                CreationTime = DateTime.UtcNow
            },
            Guid.NewGuid(),
            "a@b.com",
            tenantId: null);
    }

    private class StaticEmailContentProvider : INotificationEmailContentProvider
    {
        private readonly Func<NotificationEmailBuildContext, NotificationEmail?> _build;

        public int Order { get; }

        public StaticEmailContentProvider(
            int order,
            Func<NotificationEmailBuildContext, NotificationEmail?> build)
        {
            Order = order;
            _build = build;
        }

        public Task<NotificationEmail?> BuildOrNullAsync(
            NotificationEmailBuildContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_build(context));
        }
    }

    private class CapturingEmailBuilder : INotificationEmailBuilder
    {
        public List<NotificationEmailBuildContext> Contexts { get; } = new();

        public List<string> Cultures { get; } = new();

        public Task<NotificationEmail?> BuildAsync(
            NotificationEmailBuildContext context,
            CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            Cultures.Add(CultureInfo.CurrentUICulture.Name);
            return Task.FromResult<NotificationEmail?>(
                new NotificationEmail(context.Notification.NotificationName, context.UserId.ToString()));
        }
    }

    private sealed class ThrowingEmailBuilder : INotificationEmailBuilder
    {
        public Task<NotificationEmail?> BuildAsync(
            NotificationEmailBuildContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("content build failed");
        }
    }

    private class TestStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly IStringLocalizer _localizer = new TestStringLocalizer();

        public IStringLocalizer Create(Type resourceSource)
        {
            return _localizer;
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            return _localizer;
        }
    }

    private class TestStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, $"localized:{name}");

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(
                CultureInfo.InvariantCulture,
                "localized:{0}:{1}",
                new object[] { name, string.Join(",", arguments.Select(x => x?.ToString())) }));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return Enumerable.Empty<LocalizedString>();
        }
    }
}
