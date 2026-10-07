using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Dignite.Abp.Notifications.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Core's delivery handler constructs only the notifier registered for the event's channel — never every channel's
/// notifier and its dependency graph — so one channel's notifier failing to construct cannot break another channel.
/// </summary>
public class NotificationDeliveryRequestedHandler_Tests
{
    private const string RecordingChannel = "Recording";
    private const string BrokenChannel = "Broken";
    private const string FailingChannel = "Failing";
    private const string MisnamedChannel = "Misnamed";

    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class FakeChannelsTestModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<NotifierConstructionLog>();
            context.Services.AddSingleton<ILoggerProvider, ErrorCapturingLoggerProvider>();
            context.Services.AddTransient<RecordingNotifier>();
            context.Services.AddTransient<ConstructorThrowingNotifier>();
            context.Services.AddTransient<DeliveryThrowingNotifier>();
            context.Services.AddTransient<MisnamedNotifier>();

            Configure<NotificationNotifierOptions>(options =>
            {
                options.Notifiers.Add<RecordingNotifier>(RecordingChannel);
                options.Notifiers.Add<ConstructorThrowingNotifier>(BrokenChannel);
                options.Notifiers.Add<DeliveryThrowingNotifier>(FailingChannel);
                options.Notifiers.Add<MisnamedNotifier>(MisnamedChannel);
            });
        }
    }

    /// <summary>The real Email and Push channels installed side by side, with a push notifier that cannot be built.</summary>
    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpNotificationsEmailingModule),
        typeof(AbpNotificationsPushModule),
        typeof(AbpAutofacModule))]
    public class EmailAndBrokenPushTestModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<NotifierConstructionLog>();
            context.Services.AddSingleton<ILoggerProvider, ErrorCapturingLoggerProvider>();
            context.Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IEmailSender>()));

            var addressResolver = Substitute.For<IEmailNotificationAddressResolver>();
            addressResolver.GetEmailOrNullAsync(
                    Arg.Any<EmailNotificationAddressResolveContext>(),
                    Arg.Any<CancellationToken>())
                .Returns(EmailNotificationAddress.To("a@b.com"));
            context.Services.AddSingleton(addressResolver);

            // The ABP way to customize a channel: replace its registered notifier type, not register a second one.
            context.Services.Replace(ServiceDescriptor.Transient<PushNotifier, ConstructorThrowingPushNotifier>());
        }
    }

    [Fact]
    public async Task Delivery_constructs_only_the_notifier_registered_for_its_channel()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<NotificationDeliveryRequestedHandler>()
            .HandleEventAsync(CreateRequest(services, RecordingChannel));

        services.GetRequiredService<NotifierConstructionLog>().Types
            .ShouldBe(new[] { typeof(RecordingNotifier) });
        services.GetRequiredService<NotifierConstructionLog>().Deliveries.Single().Channel
            .ShouldBe(RecordingChannel);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task Channel_lookup_is_case_insensitive()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<NotificationDeliveryRequestedHandler>()
            .HandleEventAsync(CreateRequest(services, RecordingChannel.ToUpperInvariant()));

        services.GetRequiredService<NotifierConstructionLog>().Deliveries.Count.ShouldBe(1);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_channel_not_hosted_by_this_process_is_ignored_without_constructing_any_notifier()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<NotificationDeliveryRequestedHandler>()
            .HandleEventAsync(CreateRequest(services, "Sms"));

        services.GetRequiredService<NotifierConstructionLog>().Types.ShouldBeEmpty();

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_notifier_that_cannot_be_constructed_is_logged_and_dropped_without_affecting_other_channels()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var handler = services.GetRequiredService<NotificationDeliveryRequestedHandler>();

        // Best-effort: the construction failure must not escape into the event bus — it is logged instead.
        await handler.HandleEventAsync(CreateRequest(services, BrokenChannel));
        services.GetRequiredService<NotifierConstructionLog>().Types
            .ShouldBe(new[] { typeof(ConstructorThrowingNotifier) });
        ShouldContainConstructionFailure(LoggedErrors(services).ShouldHaveSingleItem());

        await handler.HandleEventAsync(CreateRequest(services, RecordingChannel));
        services.GetRequiredService<NotifierConstructionLog>().Deliveries.Single().Channel
            .ShouldBe(RecordingChannel);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_failing_delivery_is_logged_and_dropped()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<NotificationDeliveryRequestedHandler>()
            .HandleEventAsync(CreateRequest(services, FailingChannel));

        services.GetRequiredService<NotifierConstructionLog>().Types
            .ShouldBe(new[] { typeof(DeliveryThrowingNotifier) });

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_notifier_registered_under_a_channel_other_than_its_name_is_not_delivered_through()
    {
        using var application = await CreateApplicationAsync<FakeChannelsTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        // Rejected as a misregistration (logged), not delivered under a name it does not answer to — and not thrown.
        await services.GetRequiredService<NotificationDeliveryRequestedHandler>()
            .HandleEventAsync(CreateRequest(services, MisnamedChannel));

        services.GetRequiredService<NotifierConstructionLog>().Deliveries.ShouldBeEmpty();
        LoggedErrors(services).ShouldHaveSingleItem().Message.ShouldContain(MisnamedChannel);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task An_email_delivery_does_not_construct_the_push_notifier()
    {
        using var application = await CreateApplicationAsync<EmailAndBrokenPushTestModule>();
        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var handler = services.GetRequiredService<NotificationDeliveryRequestedHandler>();

        await handler.HandleEventAsync(CreateRequest(services, EmailNotifier.ChannelName));

        services.GetRequiredService<NotifierConstructionLog>().Types.ShouldBeEmpty();
        await services.GetRequiredService<IEmailSender>().Received(1).SendAsync(
            "a@b.com", Arg.Any<string>(), "Hello", Arg.Any<bool>());

        // The push notifier really is unbuildable — only a push delivery is affected by it, and only by being dropped.
        LoggedErrors(services).ShouldBeEmpty();
        await handler.HandleEventAsync(CreateRequest(services, PushNotifier.ChannelName));
        ShouldContainConstructionFailure(LoggedErrors(services).ShouldHaveSingleItem());
        services.GetRequiredService<NotifierConstructionLog>().Types
            .ShouldBe(new[] { typeof(ConstructorThrowingPushNotifier) });

        await application.ShutdownAsync();
    }

    [Fact]
    public void Repeating_a_channel_registration_is_idempotent()
    {
        var options = new NotificationNotifierOptions();

        options.Notifiers.Add<RecordingNotifier>(RecordingChannel);
        options.Notifiers.Add<RecordingNotifier>(RecordingChannel.ToLowerInvariant());

        options.Notifiers.Count.ShouldBe(1);
        options.Notifiers[RecordingChannel.ToUpperInvariant()].ShouldBe(typeof(RecordingNotifier));
    }

    [Fact]
    public void Two_notifier_types_for_one_channel_are_rejected()
    {
        var options = new NotificationNotifierOptions();
        options.Notifiers.Add<RecordingNotifier>(RecordingChannel);

        var exception = Should.Throw<InvalidOperationException>(
            () => options.Notifiers.Add<DeliveryThrowingNotifier>(RecordingChannel.ToLowerInvariant()));

        exception.Message.ShouldContain("Multiple notification notifiers are registered for channel");
    }

    [Fact]
    public void One_notifier_type_for_two_channels_is_rejected()
    {
        var options = new NotificationNotifierOptions();
        options.Notifiers.Add<RecordingNotifier>(RecordingChannel);

        Should.Throw<InvalidOperationException>(
            () => options.Notifiers.Add<RecordingNotifier>(FailingChannel));
    }

    [Fact]
    public void Only_notifier_types_can_be_registered()
    {
        var options = new NotificationNotifierOptions();

        Should.Throw<ArgumentException>(() => options.Notifiers.Add(RecordingChannel, typeof(object)));
        Should.Throw<ArgumentException>(() => options.Notifiers.Add(" ", typeof(RecordingNotifier)));
    }

    [Fact]
    public async Task Each_built_in_channel_module_registers_its_notifier()
    {
        using var application = await CreateApplicationAsync<EmailAndBrokenPushTestModule>();

        var notifiers = application.ServiceProvider
            .GetRequiredService<IOptions<NotificationNotifierOptions>>().Value.Notifiers;
        notifiers[EmailNotifier.ChannelName].ShouldBe(typeof(EmailNotifier));
        notifiers[PushNotifier.ChannelName].ShouldBe(typeof(PushNotifier));

        await application.ShutdownAsync();
    }

    private static async Task<IAbpApplicationWithInternalServiceProvider> CreateApplicationAsync<TModule>()
        where TModule : IAbpModule
    {
        var application = await AbpApplicationFactory.CreateAsync<TModule>(options =>
        {
            options.UseAutofac();
        });
        await application.InitializeAsync();
        return application;
    }

    private static NotificationDeliveryRequestedEto CreateRequest(IServiceProvider services, string channel)
    {
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            NotificationName = "test",
            DataJson = services.GetRequiredService<INotificationDataSerializer>()
                .Serialize(new MessageNotificationData("Hello")),
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            Channel = channel
        };
    }

    private static IReadOnlyList<Exception> LoggedErrors(IServiceProvider services)
    {
        return services.GetServices<ILoggerProvider>()
            .OfType<ErrorCapturingLoggerProvider>()
            .Single()
            .Errors
            .ToList();
    }

    private static void ShouldContainConstructionFailure(Exception exception)
    {
        // The container may wrap the constructor's exception; the original must still be in the chain.
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is NotifierConstructionException)
            {
                return;
            }
        }

        throw new ShouldAssertException(
            $"Expected a {nameof(NotifierConstructionException)} in the chain of {exception.GetType().FullName}.");
    }

    /// <summary>Records the exception of every Error-level log entry, from any category.</summary>
    public class ErrorCapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Exception> Errors { get; } = new();

        public ILogger CreateLogger(string categoryName) => new ErrorCapturingLogger(Errors);

        public void Dispose()
        {
        }

        private sealed class ErrorCapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<Exception> _errors;

            public ErrorCapturingLogger(ConcurrentQueue<Exception> errors)
            {
                _errors = errors;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error && exception != null)
                {
                    _errors.Enqueue(exception);
                }
            }
        }
    }

    public class NotifierConstructionLog
    {
        public ConcurrentQueue<Type> ConstructedTypes { get; } = new();

        public ConcurrentQueue<NotificationDeliveryRequestedEto> DeliveryQueue { get; } = new();

        public IReadOnlyList<Type> Types => ConstructedTypes.ToList();

        public IReadOnlyList<NotificationDeliveryRequestedEto> Deliveries => DeliveryQueue.ToList();
    }

    public class NotifierConstructionException : Exception
    {
    }

    public class RecordingNotifier : INotificationNotifier
    {
        private readonly NotifierConstructionLog _log;

        public RecordingNotifier(NotifierConstructionLog log)
        {
            _log = log;
            log.ConstructedTypes.Enqueue(GetType());
        }

        public string Name => RecordingChannel;

        public Task DeliverAsync(NotificationDeliveryRequestedEto request, CancellationToken cancellationToken = default)
        {
            _log.DeliveryQueue.Enqueue(request);
            return Task.CompletedTask;
        }
    }

    public class ConstructorThrowingNotifier : INotificationNotifier
    {
        public ConstructorThrowingNotifier(NotifierConstructionLog log)
        {
            log.ConstructedTypes.Enqueue(GetType());
            throw new NotifierConstructionException();
        }

        public string Name => BrokenChannel;

        public Task DeliverAsync(NotificationDeliveryRequestedEto request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Unreachable.");
        }
    }

    public class DeliveryThrowingNotifier : INotificationNotifier
    {
        public DeliveryThrowingNotifier(NotifierConstructionLog log)
        {
            log.ConstructedTypes.Enqueue(GetType());
        }

        public string Name => FailingChannel;

        public Task DeliverAsync(NotificationDeliveryRequestedEto request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Delivery failed.");
        }
    }

    public class MisnamedNotifier : INotificationNotifier
    {
        public string Name => "SomethingElse";

        public Task DeliverAsync(NotificationDeliveryRequestedEto request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    // PushNotifier is conventionally registered; keep this subclass out of every other test module in the assembly.
    [DisableConventionalRegistration]
    public class ConstructorThrowingPushNotifier : PushNotifier
    {
        public ConstructorThrowingPushNotifier(
            NotifierConstructionLog log,
            IPushDeviceStore deviceStore,
            IEnumerable<IPushProvider> providers,
            INotificationPushBuilder pushBuilder,
            INotificationDataSerializer dataSerializer,
            ILogger<PushNotifier> logger,
            IOptions<NotificationPushOptions> pushOptions)
            : base(deviceStore, providers, pushBuilder, dataSerializer, logger, pushOptions)
        {
            log.ConstructedTypes.Enqueue(GetType());
            throw new NotifierConstructionException();
        }
    }
}
