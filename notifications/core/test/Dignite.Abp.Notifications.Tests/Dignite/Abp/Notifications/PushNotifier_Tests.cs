using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace Dignite.Abp.Notifications;

public class PushNotifier_Tests
{
    private static readonly NotificationDataSerializer DataSerializer =
        NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));

    [Fact]
    public void Push_notifier_exposes_the_canonical_channel_contract()
    {
        var notifier = CreateNotifier(new FakeDeviceStore());

        notifier.Name.ShouldBe(PushNotifier.ChannelName);
        typeof(PushNotifier)
            .GetCustomAttribute<ExposeServicesAttribute>()!
            .ServiceTypes
            .ShouldBe(new[] { typeof(INotificationNotifier), typeof(PushNotifier) });
    }

    [Fact]
    public async Task Rejects_a_request_for_another_channel()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(new FakeDeviceStore(new PushTarget("Expo", "t1")), provider);

        await Should.ThrowAsync<InvalidOperationException>(
            () => notifier.DeliverAsync(CreateRequest(channel: "Email")));

        provider.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sends_nothing_when_the_recipient_has_no_device()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(new FakeDeviceStore(), provider);

        await notifier.DeliverAsync(CreateRequest());

        provider.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pushes_to_every_device_of_the_recipient_with_the_inbox_keys_as_data()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "phone"), new PushTarget("Expo", "tablet")),
            provider);
        var request = CreateRequest(entityTypeName: "Demo.Order", entityId: "42");

        await notifier.DeliverAsync(request);

        provider.Sent.Select(message => message.Token).ShouldBe(new[] { "phone", "tablet" });
        var message = provider.Sent[0];
        message.Title.ShouldBeNull();
        message.Body.ShouldBe("Shipped!");
        message.Data.ShouldBe(new Dictionary<string, string>
        {
            [PushDataKeys.NotificationId] = request.NotificationId.ToString(),
            [PushDataKeys.NotificationName] = "order.shipped",
            [PushDataKeys.EntityTypeName] = "Demo.Order",
            [PushDataKeys.EntityId] = "42"
        });
    }

    [Fact]
    public async Task Omits_entity_keys_when_the_notification_is_not_about_an_entity()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(new FakeDeviceStore(new PushTarget("Expo", "phone")), provider);

        await notifier.DeliverAsync(CreateRequest());

        provider.Sent.Single().Data.Keys.ShouldBe(
            new[] { PushDataKeys.NotificationId, PushDataKeys.NotificationName },
            ignoreOrder: true);
    }

    [Fact]
    public async Task Builds_content_once_per_device_culture_and_restores_the_previous_culture()
    {
        var builder = new RecordingPushBuilder();
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(
            new FakeDeviceStore(
                new PushTarget("Expo", "fr-1", "fr-FR"),
                new PushTarget("Expo", "ja-1", "ja-JP"),
                new PushTarget("Expo", "fr-2", "fr-FR")),
            provider,
            builder);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        await notifier.DeliverAsync(CreateRequest());

        builder.Cultures.ShouldBe(new[] { "fr-FR", "ja-JP" });
        builder.Contexts.Select(context => context.CultureName).ShouldBe(new[] { "fr-FR", "ja-JP" });
        provider.Sent.Single(message => message.Token == "fr-2").Body.ShouldBe("body:fr-FR");
        provider.Sent.Single(message => message.Token == "ja-1").Body.ShouldBe("body:ja-JP");
        CultureInfo.CurrentCulture.ShouldBe(previousCulture);
        CultureInfo.CurrentUICulture.ShouldBe(previousUICulture);
    }

    [Fact]
    public async Task Uses_the_default_culture_for_devices_without_one_or_with_an_invalid_one()
    {
        var builder = new RecordingPushBuilder();
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "none"), new PushTarget("Expo", "bad", "not a culture!")),
            provider,
            builder,
            new NotificationPushOptions { DefaultCulture = "en-US" });

        await notifier.DeliverAsync(CreateRequest());

        builder.Cultures.ShouldAllBe(culture => culture == "en-US");
        provider.Sent.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Skips_devices_whose_culture_produced_no_content()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "phone")),
            provider,
            new DefaultNotificationPushBuilder(Array.Empty<INotificationPushContentProvider>()));

        await notifier.DeliverAsync(CreateRequest());

        provider.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Routes_each_device_to_the_provider_that_issued_its_token_and_skips_unknown_providers()
    {
        var expo = new FakeProvider("Expo");
        var fcm = new FakeProvider("Fcm");
        var notifier = CreateNotifier(
            new FakeDeviceStore(
                new PushTarget("expo", "e1"),
                new PushTarget("Fcm", "f1"),
                new PushTarget("Apns", "a1")),
            expo,
            fcm);

        await notifier.DeliverAsync(CreateRequest());

        expo.Sent.Select(message => message.Token).ShouldBe(new[] { "e1" });
        fcm.Sent.Select(message => message.Token).ShouldBe(new[] { "f1" });
    }

    [Fact]
    public async Task Removes_only_the_devices_a_provider_reported_dead()
    {
        var store = new FakeDeviceStore(
            new PushTarget("Expo", "alive"),
            new PushTarget("Expo", "dead"),
            new PushTarget("Expo", "rejected"));
        var provider = new FakeProvider("Expo")
        {
            Respond = message => message.Token switch
            {
                "dead" => PushSendResult.TokenInvalid(message.Token, "DeviceNotRegistered"),
                "rejected" => PushSendResult.Failed(message.Token, "MessageTooBig"),
                _ => PushSendResult.Succeeded(message.Token)
            }
        };
        var notifier = CreateNotifier(store, provider);

        await notifier.DeliverAsync(CreateRequest());

        store.Removed.ShouldBe(new[] { ("Expo", "dead") });
    }

    [Fact]
    public async Task One_failing_provider_does_not_stop_the_others_and_its_exception_surfaces_afterwards()
    {
        var failing = new FakeProvider("Expo") { Throw = new InvalidOperationException("expo down") };
        var fcm = new FakeProvider("Fcm");
        var notifier = CreateNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "e1"), new PushTarget("Fcm", "f1")),
            failing,
            fcm);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => notifier.DeliverAsync(CreateRequest()));

        exception.Message.ShouldBe("expo down");
        fcm.Sent.Select(message => message.Token).ShouldBe(new[] { "f1" });
    }

    [Fact]
    public async Task Cancellation_stops_delivery()
    {
        var provider = new FakeProvider("Expo");
        var notifier = CreateNotifier(new FakeDeviceStore(new PushTarget("Expo", "phone")), provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => notifier.DeliverAsync(CreateRequest(), cancellation.Token));

        provider.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Two_providers_claiming_the_same_name_fail_push_deliveries_but_not_construction()
    {
        // A provider clash fails push deliveries through the best-effort path instead of escaping the delivery handler.
        var first = new FakeProvider("Expo");
        var notifier = CreateNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "phone")),
            first,
            new FakeProvider("EXPO"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => notifier.DeliverAsync(CreateRequest()));

        exception.Message.ShouldContain("both claim the name");
        first.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Removes_a_dead_device_under_the_provider_name_it_was_stored_with()
    {
        var store = new FakeDeviceStore(new PushTarget("expo", "dead"));
        var provider = new FakeProvider("Expo")
        {
            Respond = message => PushSendResult.TokenInvalid(message.Token, "DeviceNotRegistered")
        };

        await CreateNotifier(store, provider).DeliverAsync(CreateRequest());

        store.Removed.ShouldBe(new[] { ("expo", "dead") });
    }

    [Fact]
    public async Task A_failure_removing_dead_devices_does_not_stop_the_other_providers()
    {
        var store = new FakeDeviceStore(new PushTarget("Expo", "dead"), new PushTarget("Fcm", "f1"))
        {
            RemoveFailure = new InvalidOperationException("db down")
        };
        var expo = new FakeProvider("Expo")
        {
            Respond = message => PushSendResult.TokenInvalid(message.Token, "DeviceNotRegistered")
        };
        var fcm = new FakeProvider("Fcm");

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateNotifier(store, expo, fcm).DeliverAsync(CreateRequest()));

        fcm.Sent.Select(message => message.Token).ShouldBe(new[] { "f1" });
    }

    [Fact]
    public async Task Logs_which_provider_failed_without_the_exception_message()
    {
        var logger = new ListLogger<PushNotifier>();
        var notifier = new PushNotifier(
            new FakeDeviceStore(new PushTarget("Expo", "e1")),
            new IPushProvider[] { new FakeProvider("Expo") { Throw = new InvalidOperationException("secret detail") } },
            CreateDefaultBuilder(),
            DataSerializer,
            logger,
            Options.Create(new NotificationPushOptions()));

        await Should.ThrowAsync<InvalidOperationException>(() => notifier.DeliverAsync(CreateRequest()));

        var entry = logger.Messages.ShouldHaveSingleItem();
        entry.ShouldContain("'Expo'");
        entry.ShouldContain(typeof(InvalidOperationException).FullName!);
        entry.ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task The_null_store_has_no_devices()
    {
        var store = new NullPushDeviceStore(NullLogger<NullPushDeviceStore>.Instance);

        (await store.GetTargetsAsync(Guid.NewGuid())).ShouldBeEmpty();
        (await store.GetTargetsAsync(Guid.NewGuid())).ShouldBeEmpty();
        await store.RemoveAsync("Expo", "token");
    }

    private static PushNotifier CreateNotifier(IPushDeviceStore store, params IPushProvider[] providers)
    {
        return CreateNotifier(store, providers, CreateDefaultBuilder());
    }

    private static PushNotifier CreateNotifier(
        IPushDeviceStore store,
        IPushProvider provider,
        INotificationPushBuilder builder,
        NotificationPushOptions? options = null)
    {
        return CreateNotifier(store, new[] { provider }, builder, options);
    }

    private static PushNotifier CreateNotifier(
        IPushDeviceStore store,
        IPushProvider[] providers,
        INotificationPushBuilder builder,
        NotificationPushOptions? options = null)
    {
        return new PushNotifier(
            store,
            providers,
            builder,
            DataSerializer,
            NullLogger<PushNotifier>.Instance,
            Options.Create(options ?? new NotificationPushOptions()));
    }

    private static DefaultNotificationPushBuilder CreateDefaultBuilder()
    {
        return new DefaultNotificationPushBuilder(new INotificationPushContentProvider[]
        {
            new MessageNotificationPushContentProvider()
        });
    }

    private static NotificationDeliveryRequestedEto CreateRequest(
        string channel = PushNotifier.ChannelName,
        string? entityTypeName = null,
        string? entityId = null)
    {
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            NotificationName = "order.shipped",
            DataJson = DataSerializer.Serialize(new MessageNotificationData("Shipped!")),
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            Channel = channel,
            EntityTypeName = entityTypeName,
            EntityId = entityId
        };
    }

    private sealed class FakeDeviceStore : IPushDeviceStore
    {
        private readonly PushTarget[] _targets;

        public List<(string Provider, string Token)> Removed { get; } = new();

        public Exception? RemoveFailure { get; init; }

        public FakeDeviceStore(params PushTarget[] targets)
        {
            _targets = targets;
        }

        public Task<IReadOnlyList<PushTarget>> GetTargetsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PushTarget>>(_targets);
        }

        public Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
        {
            if (RemoveFailure != null)
            {
                throw RemoveFailure;
            }

            Removed.Add((provider, token));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider : IPushProvider
    {
        public string Name { get; }

        public List<PushMessage> Sent { get; } = new();

        public Func<PushMessage, PushSendResult> Respond { get; init; } =
            message => PushSendResult.Succeeded(message.Token);

        public Exception? Throw { get; init; }

        public FakeProvider(string name)
        {
            Name = name;
        }

        public Task<IReadOnlyList<PushSendResult>> SendAsync(
            IReadOnlyList<PushMessage> messages,
            CancellationToken cancellationToken = default)
        {
            if (Throw != null)
            {
                throw Throw;
            }

            Sent.AddRange(messages);
            return Task.FromResult<IReadOnlyList<PushSendResult>>(messages.Select(Respond).ToList());
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Messages.Add(formatter(state, exception) + (exception == null ? string.Empty : " " + exception.Message));
            }
        }
    }

    private sealed class RecordingPushBuilder : INotificationPushBuilder
    {
        public List<string> Cultures { get; } = new();

        public List<NotificationPushBuildContext> Contexts { get; } = new();

        public Task<NotificationPushContent?> BuildAsync(
            NotificationPushBuildContext context,
            CancellationToken cancellationToken = default)
        {
            Cultures.Add(CultureInfo.CurrentUICulture.Name);
            Contexts.Add(context);
            return Task.FromResult<NotificationPushContent?>(
                new NotificationPushContent(null, $"body:{CultureInfo.CurrentUICulture.Name}"));
        }
    }
}
