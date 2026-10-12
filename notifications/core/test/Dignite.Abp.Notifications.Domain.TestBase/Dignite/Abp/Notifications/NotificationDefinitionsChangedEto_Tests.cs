using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The saver publishes the names of the definitions it inserted or changed, as ABP's <c>StaticPermissionSaver</c>
/// publishes <c>DynamicPermissionDefinitionsChangedEto</c>: once per save that wrote them, never for a skipped save or a
/// deletion.
/// Provider-agnostic: each persistence provider's test project runs it on its own database.
/// </summary>
public abstract class NotificationDefinitionsChangedEto_Tests<TInfrastructure>
    where TInfrastructure : SharedDefinitionStoreInfrastructure, new()
{
    [Fact]
    public async Task Saving_new_definitions_publishes_their_names_and_an_unchanged_save_publishes_nothing()
    {
        using var shared = new TInfrastructure();
        await using var publisherA =
            await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared);

        await publisherA.SaveStaticDefinitionsAsync();

        var received = publisherA.Get<ReceivedDefinitionChanges>().Events;
        received.ShouldHaveSingleItem().Notifications.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(new[]
        {
            PublisherADefinitionProvider.OrderCancelled,
            PublisherADefinitionProvider.OrderShipped
        });

        await publisherA.SaveStaticDefinitionsAsync();

        received.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_changed_definition_publishes_only_its_name_and_a_deletion_publishes_nothing()
    {
        using var shared = new TInfrastructure();
        await using (var publisherA =
                     await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        // Another version of OrderShipped; OrderCancelled is not defined here and stays as it is.
        await using (var changed =
                     await DefinitionStoreTestApplication.StartAsync<NotificationServiceWithLocalCopyTestModule>(
                         "PublisherA", shared))
        {
            await changed.SaveStaticDefinitionsAsync();

            changed.Get<ReceivedDefinitionChanges>().Events.ShouldHaveSingleItem().Notifications
                .ShouldBe(new[] { PublisherADefinitionProvider.OrderShipped });
        }

        await using var deleting = await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>(
            "PublisherA",
            shared,
            services => services.Configure<NotificationDefinitionStoreOptions>(options =>
                options.DeletedNotifications.Add(PublisherADefinitionProvider.OrderCancelled)));
        await deleting.SaveStaticDefinitionsAsync();

        (await deleting.GetStoredNotificationNamesAsync()).ShouldNotContain(PublisherADefinitionProvider.OrderCancelled);
        deleting.Get<ReceivedDefinitionChanges>().Events.ShouldBeEmpty();
    }

    [Fact]
    public void The_event_has_a_stable_name_and_round_trips_through_plain_System_Text_Json()
    {
        EventNameAttribute.GetNameOrDefault<NotificationDefinitionsChangedEto>()
            .ShouldBe("Dignite.Abp.Notifications.NotificationDefinitionsChanged");

        var json = JsonSerializer.Serialize(new NotificationDefinitionsChangedEto { Notifications = { "A", "B" } });
        JsonSerializer.Deserialize<NotificationDefinitionsChangedEto>(json)!.Notifications.ShouldBe(new[] { "A", "B" });
    }
}

/// <summary>The definition-changed events one test application received through its distributed event bus.</summary>
public class ReceivedDefinitionChanges
{
    public ConcurrentQueue<NotificationDefinitionsChangedEto> Events { get; } = new();
}

public class NotificationDefinitionsChangedRecorder :
    IDistributedEventHandler<NotificationDefinitionsChangedEto>,
    ITransientDependency
{
    private readonly ReceivedDefinitionChanges _received;

    public NotificationDefinitionsChangedRecorder(ReceivedDefinitionChanges received)
    {
        _received = received;
    }

    public Task HandleEventAsync(NotificationDefinitionsChangedEto eventData)
    {
        _received.Events.Enqueue(eventData);
        return Task.CompletedTask;
    }
}
