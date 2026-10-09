using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Dignite.Abp.Notifications.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationChannelRouting_Tests
{
    // --- NotificationRoutingOptions

    [Fact]
    public void ForNotification_requires_at_least_one_channel()
    {
        Should.Throw<ArgumentException>(() => new NotificationRoutingOptions().ForNotification("test"));
    }

    [Fact]
    public void ForNotification_rejects_blank_channel_names()
    {
        Should.Throw<ArgumentException>(() => new NotificationRoutingOptions().ForNotification("test", "SignalR", " "));
    }

    [Fact]
    public void Later_ForNotification_replaces_the_earlier_rule_without_merging()
    {
        var options = new NotificationRoutingOptions()
            .ForNotification("test", "SignalR", "Email")
            .ForNotification("test", "Push");

        options.Notifications["test"].ShouldBe(new[] { "Push" });
    }

    [Fact]
    public void InboxOnly_after_ForNotification_leaves_an_empty_rule_and_the_reverse_leaves_channels()
    {
        var inboxLast = new NotificationRoutingOptions()
            .ForNotification("test", "SignalR")
            .InboxOnly("test");
        var channelsLast = new NotificationRoutingOptions()
            .InboxOnly("test")
            .ForNotification("test", "SignalR");

        inboxLast.Notifications["test"].ShouldBeEmpty();
        channelsLast.Notifications["test"].ShouldBe(new[] { "SignalR" });
    }

    [Fact]
    public void ForNotifications_expands_into_one_rule_per_name()
    {
        var options = new NotificationRoutingOptions().ForNotifications(new[] { "a", "b" }, "SignalR");

        options.Notifications.Keys.ShouldBe(new[] { "a", "b" }, ignoreOrder: true);
        options.Notifications["a"].ShouldBe(new[] { "SignalR" });
        options.Notifications["b"].ShouldBe(new[] { "SignalR" });
    }

    [Fact]
    public void Channel_names_are_deduplicated_ignoring_case_but_notification_names_are_case_sensitive()
    {
        var options = new NotificationRoutingOptions()
            .ForNotification("Test", "signalr", "SignalR", " SIGNALR ")
            .ForNotification("test", "Email");

        options.Notifications["Test"].ShouldBe(new[] { "signalr" });
        options.Notifications.Count.ShouldBe(2);
        options.Notifications["test"].ShouldBe(new[] { "Email" });
    }

    // --- DefaultNotificationChannelResolver

    [Fact]
    public async Task Rule_wins_over_Default_and_InboxOnly_resolves_to_null_even_with_a_Default()
    {
        var options = new NotificationRoutingOptions { Default = new[] { "SignalR" } }
            .ForNotification("ruled", "Email")
            .InboxOnly("inbox");
        var resolver = CreateResolver(options);

        (await resolver.ResolveAsync(Definition("ruled"), Notification("ruled"))).ShouldBe(new[] { "Email" });
        (await resolver.ResolveAsync(Definition("inbox"), Notification("inbox"))).ShouldBeNull();
    }

    [Fact]
    public async Task No_rule_resolves_to_Default_and_an_empty_Default_resolves_to_null()
    {
        var withDefault = CreateResolver(new NotificationRoutingOptions { Default = new[] { "SignalR", "signalr" } });
        var emptyDefault = CreateResolver(new NotificationRoutingOptions { Default = Array.Empty<string>() });
        var noDefault = CreateResolver(new NotificationRoutingOptions());

        (await withDefault.ResolveAsync(Definition("x"), Notification("x"))).ShouldBe(new[] { "SignalR" });
        (await emptyDefault.ResolveAsync(Definition("x"), Notification("x"))).ShouldBeNull();
        (await noDefault.ResolveAsync(Definition("x"), Notification("x"))).ShouldBeNull();
    }

    // --- Distributor wiring

    [Fact]
    public void SignalR_notifier_exposes_the_canonical_channel_contract()
    {
        var notifier = new SignalRNotifier(Substitute.For<IHubContext<NotificationsHub>>());

        notifier.Name.ShouldBe(SignalRNotifier.ChannelName);
        notifier.ShouldBeAssignableTo<INotificationNotifier>();

        var exposedServices = typeof(SignalRNotifier)
            .GetCustomAttribute<ExposeServicesAttribute>()!
            .ServiceTypes;
        exposedServices.Count(type => type == typeof(INotificationNotifier)).ShouldBe(1);
        exposedServices.ShouldBe(new[] { typeof(INotificationNotifier), typeof(SignalRNotifier) });
    }

    [Fact]
    public async Task Distributor_sets_eto_channels_from_the_resolver()
    {
        var published = new List<NotificationDeliveryRequestedEto>();
        var distributor = CreateDistributor(
            CreateResolver(new NotificationRoutingOptions().ForNotification("test", EmailNotifier.ChannelName, SignalRNotifier.ChannelName)),
            published);

        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" }, new[] { Guid.NewGuid() });

        published.Count.ShouldBe(2);
        published.Select(item => item.Channel)
            .ShouldBe(new[] { EmailNotifier.ChannelName, SignalRNotifier.ChannelName }, ignoreOrder: true);
        published.Select(item => item.UserId).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Distributor_resolves_channels_once_per_distribution_regardless_of_recipient_count()
    {
        var resolver = Substitute.For<INotificationChannelResolver>();
        resolver.ResolveAsync(Arg.Any<NotificationDefinition>(), Arg.Any<NotificationInfo>(), Arg.Any<CancellationToken>())
            .Returns(new string[] { "SignalR" });
        var published = new List<NotificationDeliveryRequestedEto>();
        var distributor = CreateDistributor(resolver, published, new NotificationDistributionOptions { RecipientBatchSize = 2 });

        var recipients = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" }, recipients);

        published.Count.ShouldBe(7);
        await resolver.Received(1).ResolveAsync(
            Arg.Any<NotificationDefinition>(), Arg.Any<NotificationInfo>(), Arg.Any<CancellationToken>());
    }

    // --- Startup validation

    [Fact]
    public async Task Startup_fails_listing_every_rule_for_an_unknown_notification()
    {
        var options = new NotificationRoutingOptions()
            .ForNotification("known", "SignalR")
            .ForNotification("typo.one", "SignalR")
            .InboxOnly("typo.two");

        var exception = await Should.ThrowAsync<AbpException>(() =>
            CreateStartup(new[] { "known" }, options, hostedChannels: new[] { "SignalR" }).StartingAsync(default));

        exception.Message.ShouldContain("'typo.one'");
        exception.Message.ShouldContain("'typo.two'");
        exception.Message.ShouldNotContain("'known'");
    }

    [Fact]
    public async Task Startup_warns_by_default_about_an_unhosted_channel_and_names_the_notifications()
    {
        var logger = new CapturingLogger();
        var options = new NotificationRoutingOptions { Default = new[] { "Push" } }
            .ForNotifications(new[] { "a", "b" }, "Push", "SignalR");

        await CreateStartup(new[] { "a", "b" }, options, hostedChannels: new[] { "SignalR" }, logger: logger)
            .StartingAsync(default);

        var warning = logger.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("Push");
        warning.Message.ShouldContain("a, b");
        warning.Message.ShouldNotContain("SignalR");
    }

    [Fact]
    public async Task Startup_fails_on_an_unhosted_channel_when_RequireHostedChannels_is_set()
    {
        var options = new NotificationRoutingOptions { RequireHostedChannels = true }
            .ForNotification("a", "Push");

        var exception = await Should.ThrowAsync<AbpException>(() =>
            CreateStartup(new[] { "a" }, options, hostedChannels: new[] { "SignalR" }).StartingAsync(default));

        exception.Message.ShouldContain("'Push'");
        exception.Message.ShouldContain("a");
    }

    [Fact]
    public async Task Startup_matches_hosted_channels_ignoring_case()
    {
        var logger = new CapturingLogger();
        var options = new NotificationRoutingOptions { RequireHostedChannels = true }
            .ForNotification("a", "signalr");

        await CreateStartup(new[] { "a" }, options, hostedChannels: new[] { "SignalR" }, logger: logger)
            .StartingAsync(default);

        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Startup_fails_in_stateless_mode_when_a_definition_resolves_to_no_channel()
    {
        var options = new NotificationRoutingOptions()
            .ForNotification("routed", "SignalR")
            .InboxOnly("inbox");

        var exception = await Should.ThrowAsync<AbpException>(() =>
            CreateStartup(
                new[] { "routed", "inbox", "unruled" },
                options,
                hostedChannels: new[] { "SignalR" },
                store: new NullNotificationStore()).StartingAsync(default));

        exception.Message.ShouldContain("'inbox'");
        exception.Message.ShouldContain("'unruled'");
        exception.Message.ShouldNotContain("'routed'");
    }

    [Fact]
    public async Task Startup_does_not_fail_in_stateless_mode_when_a_Default_covers_every_definition()
    {
        var options = new NotificationRoutingOptions { Default = new[] { "SignalR" } };

        await CreateStartup(
            new[] { "a", "b" },
            options,
            hostedChannels: new[] { "SignalR" },
            store: new NullNotificationStore()).StartingAsync(default);
    }

    [Fact]
    public async Task Startup_skips_the_stateless_check_when_the_resolver_is_replaced()
    {
        var customResolver = Substitute.For<INotificationChannelResolver>();

        await CreateStartup(
            new[] { "unruled" },
            new NotificationRoutingOptions(),
            hostedChannels: Array.Empty<string>(),
            store: new NullNotificationStore(),
            resolver: customResolver).StartingAsync(default);

        await customResolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default);
    }

    [Fact]
    public async Task Startup_does_not_require_channels_when_an_inbox_store_is_installed()
    {
        await CreateStartup(
            new[] { "unruled" },
            new NotificationRoutingOptions(),
            hostedChannels: Array.Empty<string>()).StartingAsync(default);
    }

    private static NotificationDefinition Definition(string name)
    {
        return new NotificationDefinition("Test", name, new FixedLocalizableString(name));
    }

    private static NotificationInfo Notification(string name)
    {
        return new NotificationInfo { Id = Guid.NewGuid(), NotificationName = name };
    }

    private static DefaultNotificationChannelResolver CreateResolver(NotificationRoutingOptions options)
    {
        return new DefaultNotificationChannelResolver(Options.Create(options));
    }

    private static DefaultNotificationDistributor CreateDistributor(
        INotificationChannelResolver resolver,
        List<NotificationDeliveryRequestedEto> published,
        NotificationDistributionOptions? distributionOptions = null)
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();

        definitionManager.GetAsync("test").Returns(Definition("test"));
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(ci => published.Add(ci.Arg<NotificationDeliveryRequestedEto>()));

        return new DefaultNotificationDistributor(
            store,
            definitionManager,
            resolver,
            eventBus,
            new TestCurrentTenant(),
            NullLogger<DefaultNotificationDistributor>.Instance,
            Options.Create(distributionOptions ?? new NotificationDistributionOptions()));
    }

    /// <summary>
    /// The startup checks of a host that distributes: Core's routing-name check followed by Distribution's hosted-channel
    /// and stateless-mode checks, in the order the hosted services run.
    /// </summary>
    private static StartupChecks CreateStartup(
        string[] definitionNames,
        NotificationRoutingOptions routing,
        string[] hostedChannels,
        INotificationStore? store = null,
        INotificationChannelResolver? resolver = null,
        ILogger<NotificationDistributionStartupService>? logger = null)
    {
        var staticStore = Substitute.For<IStaticNotificationDefinitionStore>();
        staticStore.GetNotificationsAsync().Returns(definitionNames.Select(Definition).ToList());

        var notifierOptions = new NotificationNotifierOptions();
        foreach (var channel in hostedChannels)
        {
            notifierOptions.Notifiers.Add(channel, typeof(FakeNotifier));
        }

        var services = new ServiceCollection();
        services.AddSingleton(store ?? Substitute.For<INotificationStore>());
        services.AddSingleton(resolver ?? CreateResolver(routing));
        var provider = services.BuildServiceProvider();

        return new StartupChecks(
            new NotificationDefinitionStartupService(
                staticStore,
                Options.Create(new NotificationDefinitionRegistration()),
                NotificationTestObjects.CreateRegistry(),
                Options.Create(routing)),
            new NotificationDistributionStartupService(
                staticStore,
                Options.Create(routing),
                Options.Create(notifierOptions),
                provider,
                logger ?? NullLogger<NotificationDistributionStartupService>.Instance));
    }

    private sealed class StartupChecks
    {
        private readonly NotificationDefinitionStartupService _core;
        private readonly NotificationDistributionStartupService _distribution;

        public StartupChecks(NotificationDefinitionStartupService core, NotificationDistributionStartupService distribution)
        {
            _core = core;
            _distribution = distribution;
        }

        public async Task StartingAsync(CancellationToken cancellationToken)
        {
            await _core.StartingAsync(cancellationToken);
            await _distribution.StartingAsync(cancellationToken);
        }
    }

    private sealed class FakeNotifier : INotificationNotifier
    {
        public string Name => "Fake";

        public Task DeliverAsync(NotificationDeliveryRequestedEto request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger : ILogger<NotificationDistributionStartupService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
