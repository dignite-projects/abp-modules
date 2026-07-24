using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Dignite.Abp.Notifications.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationChannelRouting_Tests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "SignalR" }, true)]
    [InlineData(new[] { "signalr" }, true)] // case-insensitive
    [InlineData(new[] { "Email" }, false)]
    [InlineData(new[] { "Email", "Other" }, false)]
    public void IsAllowed_respects_the_channel_white_list(string[]? channels, bool expected)
    {
        NotificationChannels.IsAllowed(channels, "SignalR").ShouldBe(expected);
    }

    [Fact]
    public void UseChannels_requires_at_least_one_channel()
    {
        Should.Throw<ArgumentException>(() =>
            new NotificationDefinition("test", new FixedLocalizableString("Test")).UseChannels());
    }

    [Fact]
    public void UseChannels_rejects_blank_channel_names()
    {
        Should.Throw<ArgumentException>(() =>
            new NotificationDefinition("test", new FixedLocalizableString("Test")).UseChannels("SignalR", " "));
    }

    [Fact]
    public void SignalR_notifier_exposes_the_canonical_channel_contract()
    {
        var notifier = new SignalRNotifier(
            Substitute.For<IHubContext<NotificationsHub>>(),
            NotificationTestObjects.CreateSerializer());

        notifier.Name.ShouldBe(SignalRNotifier.ChannelName);
        notifier.ShouldBeAssignableTo<INotificationNotifier>();

        var exposedServices = typeof(SignalRNotifier)
            .GetCustomAttribute<ExposeServicesAttribute>()!
            .ServiceTypes;
        exposedServices.Count(type => type == typeof(INotificationNotifier)).ShouldBe(1);
        exposedServices.ShouldBe(new[] { typeof(INotificationNotifier), typeof(SignalRNotifier) });
    }

    [Fact]
    public async Task Distributor_sets_eto_channels_from_the_definition()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();

        definitionManager.Get("test").Returns(
            new NotificationDefinition("test", new FixedLocalizableString("Test"))
                .UseChannels(EmailNotifier.ChannelName, SignalRNotifier.ChannelName));
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

        var published = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(ci => published.Add(ci.Arg<NotificationDeliveryRequestedEto>()));

        var currentTenant = new TestCurrentTenant();
        var distributor = new DefaultNotificationDistributor(
            store,
            definitionManager,
            eventBus,
            NotificationTestObjects.CreateSerializer(),
            currentTenant,
            NullLogger<DefaultNotificationDistributor>.Instance,
            Options.Create(new NotificationDistributionOptions()));
        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" }, new[] { Guid.NewGuid() });

        published.Count.ShouldBe(2);
        published.Select(item => item.Channel)
            .ShouldBe(new[] { EmailNotifier.ChannelName, SignalRNotifier.ChannelName }, ignoreOrder: true);
        published.Select(item => item.UserId).Distinct().Count().ShouldBe(1);
    }

}
