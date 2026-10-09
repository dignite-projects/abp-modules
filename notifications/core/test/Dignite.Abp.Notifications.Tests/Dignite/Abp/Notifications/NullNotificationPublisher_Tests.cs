using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A process with only the contracts — a business module started without any pipeline — still resolves
/// <see cref="INotificationPublisher"/>: every publish is logged as a warning and dropped, as ABP's null objects do.
/// </summary>
public class NullNotificationPublisher_Tests
{
    [Fact]
    public async Task With_no_pipeline_installed_the_null_publisher_resolves_and_warns_on_publish()
    {
        using var application = await AbpApplicationFactory.CreateAsync<AbstractionsOnlyHostModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();

        var publisher = application.ServiceProvider.GetRequiredService<INotificationPublisher>();
        publisher.ShouldBeOfType<NullNotificationPublisher>();

        await publisher.PublishAsync("Test.NotInstalled", new MessageNotificationData("dropped"), userIds: new[] { Guid.NewGuid() });

        var warning = application.ServiceProvider.GetRequiredService<CapturingLoggerProvider>().Entries
            .Where(entry => entry.Category == typeof(NullNotificationPublisher).FullName)
            .ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("Test.NotInstalled");

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_publisher_registered_by_any_module_wins_over_the_null_publisher()
    {
        using var application = await AbpApplicationFactory.CreateAsync<OwnPublisherHostModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();

        application.ServiceProvider.GetServices<INotificationPublisher>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<OwnPublisher>();

        await application.ShutdownAsync();
    }

    public class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;
            private readonly string _category;

            public CapturingLogger(CapturingLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _provider.Entries.Enqueue((_category, logLevel, formatter(state, exception)));
            }
        }
    }

    [DependsOn(
        typeof(AbpNotificationsAbstractionsModule),
        typeof(AbpAutofacModule))]
    public class AbstractionsOnlyHostModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var loggerProvider = new CapturingLoggerProvider();
            context.Services.AddSingleton(loggerProvider);
            context.Services.AddSingleton<ILoggerProvider>(loggerProvider);
        }
    }

    [DependsOn(
        typeof(AbpNotificationsAbstractionsModule),
        typeof(AbpAutofacModule))]
    public class OwnPublisherHostModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient<INotificationPublisher, OwnPublisher>();
        }
    }

    public class OwnPublisher : INotificationPublisher
    {
        public Task PublishAsync(
            string notificationName,
            NotificationData? data = null,
            NotificationEntityIdentifier? entityIdentifier = null,
            NotificationSeverity severity = NotificationSeverity.Info,
            Guid[]? userIds = null,
            Guid[]? excludedUserIds = null)
        {
            return Task.CompletedTask;
        }
    }
}
