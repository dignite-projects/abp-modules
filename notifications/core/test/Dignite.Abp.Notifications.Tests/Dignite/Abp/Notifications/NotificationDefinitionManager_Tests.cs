using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// How the manager merges this process's definitions with those of a definition store: static first, a dynamic
/// definition or group only under a name no static one has (ABP's <c>FeatureDefinitionManager</c>).
/// </summary>
public class NotificationDefinitionManager_Tests : DigniteAbpNotificationsTestBase
{
    [Fact]
    public void Without_a_definition_store_the_dynamic_source_is_empty()
    {
        GetRequiredService<IDynamicNotificationDefinitionStore>().ShouldBeOfType<NullDynamicNotificationDefinitionStore>();
    }

    [Fact]
    public async Task A_static_definition_wins_over_a_dynamic_one_of_the_same_name()
    {
        var manager = CreateManager(out var staticDefinitions, out var dynamicDefinitions);

        var definition = await manager.GetAsync("Shared.Name");

        definition.ShouldBeSameAs(staticDefinitions.Single(d => d.Name == "Shared.Name"));
        definition.ShouldNotBeSameAs(dynamicDefinitions.Single(d => d.Name == "Shared.Name"));
    }

    [Fact]
    public async Task A_name_only_the_definition_store_has_is_found_there()
    {
        var manager = CreateManager(out _, out var dynamicDefinitions);

        (await manager.GetOrNullAsync("Remote.Only")).ShouldBeSameAs(dynamicDefinitions.Single(d => d.Name == "Remote.Only"));
        (await manager.GetOrNullAsync("remote.only")).ShouldBeNull();
        (await manager.GetOrNullAsync("Nobody.Defines.This")).ShouldBeNull();
        await Should.ThrowAsync<AbpException>(() => manager.GetAsync("Nobody.Defines.This"));
    }

    [Fact]
    public async Task All_definitions_list_the_static_ones_first_and_each_name_once()
    {
        var manager = CreateManager(out _, out _);

        (await manager.GetAllAsync()).Select(d => d.Name)
            .ShouldBe(new[] { "Local.First", "Shared.Name", "Remote.Only" });
    }

    [Fact]
    public async Task Groups_list_the_static_ones_first_and_each_name_once()
    {
        var manager = CreateManager(out _, out _);

        (await manager.GetGroupsAsync()).Select(g => g.Name).ShouldBe(new[] { "Local", "Shared", "Remote" });
        (await manager.GetGroupOrNullAsync("Shared"))!.DisplayName.ShouldBeOfType<FixedLocalizableString>()
            .Value.ShouldBe("Shared (static)");
        (await manager.GetGroupOrNullAsync("Remote")).ShouldNotBeNull();
        (await manager.GetGroupOrNullAsync("remote")).ShouldBeNull();
    }

    [Fact]
    public async Task Availability_applies_the_requirements_of_the_definition_that_wins()
    {
        var manager = CreateManager(out _, out _);
        var userId = Guid.NewGuid();

        // The dynamic copy of Shared.Name requires a permission the test checker denies; the static copy does not.
        (await manager.IsAvailableAsync("Shared.Name", userId)).ShouldBeTrue();
        // Requirements of a definition only the store has apply as they were saved.
        (await manager.IsAvailableAsync("Remote.Only", userId)).ShouldBeFalse();
        // An unknown name is never available.
        (await manager.IsAvailableAsync("Nobody.Defines.This", userId)).ShouldBeFalse();
        (await manager.GetAllAvailableAsync(userId)).Select(d => d.Name).ShouldBe(new[] { "Local.First", "Shared.Name" });
    }

    private NotificationDefinitionManager CreateManager(
        out IReadOnlyList<NotificationDefinition> staticDefinitions,
        out IReadOnlyList<NotificationDefinition> dynamicDefinitions)
    {
        var local = new NotificationDefinitionContext();
        local.AddGroup("Local").AddNotification("Local.First", new FixedLocalizableString("Local first"));
        local.AddGroup("Shared", new FixedLocalizableString("Shared (static)"))
            .AddNotification("Shared.Name", new FixedLocalizableString("Shared (static)"));

        var remote = new NotificationDefinitionContext();
        remote.AddGroup("Shared", new FixedLocalizableString("Shared (dynamic)"))
            .AddNotification("Shared.Name", new FixedLocalizableString("Shared (dynamic)"))
            .RequirePermission(TestNotificationPermissionChecker.DeniedPermission);
        remote.AddGroup("Remote")
            .AddNotification("Remote.Only", new FixedLocalizableString("Remote only"))
            .RequirePermission(TestNotificationPermissionChecker.DeniedPermission);

        staticDefinitions = local.Groups.SelectMany(g => g.Notifications).ToList();
        dynamicDefinitions = remote.Groups.SelectMany(g => g.Notifications).ToList();

        var staticStore = Substitute.For<IStaticNotificationDefinitionStore>();
        StubStore(staticStore, local.Groups);
        var dynamicStore = Substitute.For<IDynamicNotificationDefinitionStore>();
        StubStore(dynamicStore, remote.Groups);

        return new NotificationDefinitionManager(
            staticStore,
            dynamicStore,
            GetRequiredService<IServiceScopeFactory>());
    }

    private static void StubStore(IStaticNotificationDefinitionStore store, IReadOnlyList<NotificationGroupDefinition> groups)
    {
        var definitions = groups.SelectMany(g => g.Notifications).ToList();
        store.GetGroupsAsync().Returns(groups);
        store.GetNotificationsAsync().Returns(definitions);
        store.GetOrNullAsync(Arg.Any<string>())
            .Returns(call => definitions.FirstOrDefault(d => d.Name == call.Arg<string>()));
    }

    private static void StubStore(IDynamicNotificationDefinitionStore store, IReadOnlyList<NotificationGroupDefinition> groups)
    {
        var definitions = groups.SelectMany(g => g.Notifications).ToList();
        store.GetGroupsAsync().Returns(groups);
        store.GetNotificationsAsync().Returns(definitions);
        store.GetOrNullAsync(Arg.Any<string>())
            .Returns(call => definitions.FirstOrDefault(d => d.Name == call.Arg<string>()));
    }
}
