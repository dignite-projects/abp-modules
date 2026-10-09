using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.TestProviderA;
using Dignite.Abp.Notifications.TestProviderB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationRegistration_Tests
{
    [Fact]
    public void Duplicate_data_discriminator_reports_both_types_independent_of_registration_order()
    {
        var forward = Should.Throw<InvalidOperationException>(() =>
        {
            var options = new NotificationDataOptions();
            options.Add<DuplicateDataA>();
            options.Add<DuplicateDataB>();
        });
        var reverse = Should.Throw<InvalidOperationException>(() =>
        {
            var options = new NotificationDataOptions();
            options.Add<DuplicateDataB>();
            options.Add<DuplicateDataA>();
        });

        forward.Message.ShouldBe(reverse.Message);
        forward.Message.ShouldContain("Test.Duplicate");
        forward.Message.ShouldContain(typeof(DuplicateDataA).FullName!);
        forward.Message.ShouldContain(typeof(DuplicateDataB).FullName!);
    }

    [Fact]
    public void One_data_type_cannot_be_registered_under_two_discriminators()
    {
        var forward = Should.Throw<InvalidOperationException>(() =>
        {
            var options = new NotificationDataOptions();
            options.Add("Test.First", typeof(DistinctDataA));
            options.Add("Test.Second", typeof(DistinctDataA));
        });
        var reverse = Should.Throw<InvalidOperationException>(() =>
        {
            var options = new NotificationDataOptions();
            options.Add("Test.Second", typeof(DistinctDataA));
            options.Add("Test.First", typeof(DistinctDataA));
        });

        forward.Message.ShouldBe(reverse.Message);
        forward.Message.ShouldContain(typeof(DistinctDataA).FullName!);
        forward.Message.ShouldContain("Test.First");
        forward.Message.ShouldContain("Test.Second");
    }

    [Fact]
    public void Exact_data_mapping_repeat_is_idempotent()
    {
        var options = new NotificationDataOptions();

        options.Add<DistinctDataA>();
        options.Add<DistinctDataA>();
        options.Add("Test.DistinctA", typeof(DistinctDataA));

        options.DataTypes.Count.ShouldBe(1);
        options.DataTypes["Test.DistinctA"].ShouldBe(typeof(DistinctDataA));
    }

    [Fact]
    public void Data_types_dictionary_cannot_bypass_conflict_validation()
    {
        var discriminatorConflict = new NotificationDataOptions();
        discriminatorConflict.DataTypes["Test.Direct"] = typeof(DistinctDataA);

        Should.Throw<InvalidOperationException>(() =>
            discriminatorConflict.DataTypes["Test.Direct"] = typeof(DistinctDataB));

        var typeConflict = new NotificationDataOptions();
        typeConflict.DataTypes["Test.DirectA"] = typeof(DistinctDataA);

        Should.Throw<InvalidOperationException>(() =>
            typeConflict.DataTypes["Test.DirectB"] = typeof(DistinctDataA));
    }

    [Fact]
    public void Data_discriminator_registration_and_lookup_are_ordinal_and_case_sensitive()
    {
        var options = new NotificationDataOptions();
        options.Add<CaseSensitiveDataUpper>();
        options.Add<CaseSensitiveDataLower>();
        var registry = new NotificationDataTypeRegistry(Options.Create(options));

        registry.GetTypeOrNull("Test.Case").ShouldBe(typeof(CaseSensitiveDataUpper));
        registry.GetTypeOrNull("test.case").ShouldBe(typeof(CaseSensitiveDataLower));
        registry.GetTypeOrNull("TEST.CASE").ShouldBeNull();
        registry.GetDiscriminatorOrNull(typeof(CaseSensitiveDataUpper)).ShouldBe("Test.Case");
        registry.GetDiscriminatorOrNull(typeof(CaseSensitiveDataLower)).ShouldBe("test.case");
    }

    [Fact]
    public void Definition_names_are_ordinal_case_sensitive_and_every_exact_repeat_is_rejected()
    {
        var context = new NotificationDefinitionContext();
        var group = context.AddGroup("Test.Group");
        AddDefinition(group, "Test.Definition");
        AddDefinition(group, "test.definition");

        context.GetOrNull("Test.Definition").ShouldNotBeNull();
        context.GetOrNull("test.definition").ShouldNotBeNull();
        context.GetOrNull("TEST.DEFINITION").ShouldBeNull();

        var exception = Should.Throw<InvalidOperationException>(() =>
            AddDefinition(group, "Test.Definition"));
        exception.Message.ShouldContain("Test.Definition");
        exception.Message.ShouldContain("<direct registration>");
    }

    [Fact]
    public void Definition_names_are_unique_across_groups()
    {
        var context = new NotificationDefinitionContext();
        AddDefinition(context.AddGroup("Test.First"), "Test.Shared");

        var exception = Should.Throw<InvalidOperationException>(() =>
            AddDefinition(context.AddGroup("Test.Second"), "Test.Shared"));
        exception.Message.ShouldContain("Test.Shared");
        context.GetGroupOrNull("Test.Second")!.Notifications.ShouldBeEmpty();
    }

    [Fact]
    public void Group_names_are_ordinal_case_sensitive_and_every_repeat_is_rejected()
    {
        var context = new NotificationDefinitionContext();
        context.AddGroup("Test.Group");
        context.AddGroup("test.group");

        context.GetGroupOrNull("Test.Group").ShouldNotBeNull();
        context.GetGroupOrNull("test.group").ShouldNotBeNull();
        context.GetGroupOrNull("TEST.GROUP").ShouldBeNull();

        var exception = Should.Throw<InvalidOperationException>(() => context.AddGroup("Test.Group"));
        exception.Message.ShouldContain("Test.Group");
        exception.Message.ShouldContain("<direct registration>");
    }

    [Fact]
    public void Definitions_record_their_group_and_keep_registration_order()
    {
        var context = new NotificationDefinitionContext();
        var group = context.AddGroup("Test.Orders", new FixedLocalizableString("Orders"));
        AddDefinition(group, "Test.Order.Shipped");
        AddDefinition(group, "Test.Order.Paid");
        AddDefinition(context.GetGroupOrNull("Test.Orders")!, "Test.Order.Cancelled");

        group.Notifications.Select(definition => definition.Name)
            .ShouldBe(new[] { "Test.Order.Shipped", "Test.Order.Paid", "Test.Order.Cancelled" });
        group.Notifications.ShouldAllBe(definition => definition.GroupName == "Test.Orders");
        group.GetNotificationOrNull("Test.Order.Paid").ShouldNotBeNull();
        group.DisplayName.ShouldBeOfType<FixedLocalizableString>().Value.ShouldBe("Orders");
        context.AddGroup("Test.Unnamed").DisplayName.ShouldBeOfType<FixedLocalizableString>().Value
            .ShouldBe("Test.Unnamed");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Definition_and_group_names_reject_empty_or_whitespace_values_immediately(string name)
    {
        var context = new NotificationDefinitionContext();
        Should.Throw<ArgumentException>(() => context.AddGroup(name));
        Should.Throw<ArgumentException>(() =>
            context.AddGroup("Test.Group").AddNotification(name, new FixedLocalizableString("Invalid")));
    }

    [Fact]
    public async Task Duplicate_group_providers_fail_host_start()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<DuplicateGroupsStartupModule>());

        exception.Message.ShouldContain("Test.DuplicateGroup");
        exception.Message.ShouldContain(typeof(DuplicateGroupDefinitionProviderA).FullName!);
        exception.Message.ShouldContain(typeof(DuplicateGroupDefinitionProviderB).FullName!);
    }

    [Fact]
    public async Task Definition_manager_exposes_groups_in_registration_order()
    {
        using var host = BuildHost<GroupedDefinitionsStartupModule>();
        await host.StartAsync();
        var definitionManager = host.Services.GetRequiredService<INotificationDefinitionManager>();

        // The test assembly's convention-discovered provider contributes its own group as well.
        (await definitionManager.GetGroupsAsync()).Select(group => group.Name)
            .Where(name => name != TestNotificationDefinitionProvider.GroupName)
            .ShouldBe(new[] { "Test.Orders", "Test.System" });
        (await definitionManager.GetGroupOrNullAsync("Test.System"))!.Notifications.Single().Name
            .ShouldBe("Test.Announcement");
        (await definitionManager.GetGroupOrNullAsync("test.system")).ShouldBeNull();
        (await definitionManager.GetAllAsync())
            .Where(definition => definition.GroupName != TestNotificationDefinitionProvider.GroupName)
            .Select(definition => definition.Name)
            .ShouldBe(new[] { "Test.Order.Shipped", "Test.Order.Paid", "Test.Announcement" });
        (await definitionManager.GetAsync("Test.Order.Paid")).GroupName.ShouldBe("Test.Orders");

        await host.StopAsync();
    }

    [Fact]
    public async Task Duplicate_definition_providers_fail_host_start_independent_of_provider_order()
    {
        var forward = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<DuplicateDefinitionsForwardStartupModule>());
        var reverse = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<DuplicateDefinitionsReverseStartupModule>());

        forward.Message.ShouldBe(reverse.Message);
        forward.Message.ShouldContain("Test.CrossModuleDuplicate");
        forward.Message.ShouldContain(typeof(TestProviderADefinitionProvider).FullName!);
        forward.Message.ShouldContain(typeof(TestProviderBDefinitionProvider).FullName!);
        forward.Message.ShouldContain(typeof(TestProviderADefinitionProvider).Assembly.GetName().Name!);
        forward.Message.ShouldContain(typeof(TestProviderBDefinitionProvider).Assembly.GetName().Name!);
    }

    [Fact]
    public async Task Duplicate_data_discriminators_fail_host_start_independent_of_registration_order()
    {
        var forward = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<DuplicateDataForwardStartupModule>());
        var reverse = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<DuplicateDataReverseStartupModule>());

        forward.Message.ShouldBe(reverse.Message);
        forward.Message.ShouldContain("Test.Duplicate");
        forward.Message.ShouldContain(typeof(DuplicateDataA).FullName!);
        forward.Message.ShouldContain(typeof(DuplicateDataB).FullName!);
    }

    [Fact]
    public async Task Ambiguous_discriminators_for_one_type_fail_host_start()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => StartHostAsync<AmbiguousDataTypeStartupModule>());

        exception.Message.ShouldContain(typeof(DistinctDataA).FullName!);
        exception.Message.ShouldContain("Test.First");
        exception.Message.ShouldContain("Test.Second");
    }

    [Fact]
    public async Task Distinct_registrations_and_exact_data_repeat_allow_host_start()
    {
        await StartHostAsync<ValidRegistrationsStartupModule>();
    }

    [Fact]
    public async Task Invalid_distribution_batch_configuration_fails_host_start()
    {
        var exception = await Should.ThrowAsync<Exception>(
            () => StartHostAsync<InvalidDistributionBatchStartupModule>());

        exception.ToString().ShouldContain(nameof(NotificationDistributionOptions));
        exception.ToString().ShouldContain(nameof(NotificationDistributionOptions.RecipientBatchSize));
        exception.ToString().ShouldContain(NotificationDistributionOptions.MaxBatchSize.ToString());
    }

    [Fact]
    public void Option_groups_preserve_existing_defaults()
    {
        var distribution = new NotificationDistributionOptions();
        distribution.DirectDistributionUserThreshold.ShouldBe(5);
        distribution.RecipientBatchSize.ShouldBe(256);

        new NotificationDefinitionRegistration().DefinitionProviders.ShouldBeEmpty();
        NotificationDistributionOptions.MaxBatchSize.ShouldBe(10_000);
    }

    [Fact]
    public async Task Abstractions_only_host_registers_one_tolerant_global_converter()
    {
        using var host = BuildHost<AbstractionsOnlyStartupModule>();
        await host.StartAsync();

        var serializerOptions = host.Services
            .GetRequiredService<IOptions<AbpSystemTextJsonSerializerOptions>>()
            .Value
            .JsonSerializerOptions;

        serializerOptions.Converters
            .OfType<NotificationDataJsonConverter>()
            .Count()
            .ShouldBe(1);

        var data = JsonSerializer.Deserialize<NotificationData>(
            """{"type":"Other.Product.Payload","value":"opaque"}""",
            serializerOptions);

        var unsupported = data.ShouldBeOfType<UnsupportedNotificationData>();
        unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
        unsupported.OriginalDiscriminator.ShouldBe("Other.Product.Payload");

        await host.StopAsync();
    }

    [Fact]
    public async Task Definition_provider_can_resolve_options_and_manager_during_host_start()
    {
        await StartHostAsync<ProviderDependencyStartupModule>();
    }

    [Fact]
    public async Task Host_start_uses_the_registered_static_definition_store_override()
    {
        // The default store would fail the start: providers A and B both register Test.CrossModuleDuplicate.
        using var host = BuildHost<CustomStaticDefinitionStoreStartupModule>();
        await host.StartAsync();

        (await host.Services.GetRequiredService<INotificationDefinitionManager>().GetAllAsync())
            .Select(definition => definition.Name)
            .ShouldBe(new[] { "Test.CustomStore" });

        await host.StopAsync();
    }

    [Fact]
    public async Task Convention_discovered_definition_provider_executes_once_across_startup_and_concurrent_lookups()
    {
        TestProviderADefinitionProvider.ResetDefineCallCount();
        using var host = BuildHost<SingleDefinitionProviderStartupModule>();
        host.Services.GetService<TestProviderADefinitionProvider>().ShouldNotBeNull();

        await host.StartAsync();
        var definitionManager = host.Services.GetRequiredService<INotificationDefinitionManager>();
        await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => definitionManager.GetAllAsync())));

        TestProviderADefinitionProvider.DefineCallCount.ShouldBe(1);
        await host.StopAsync();
    }

    private static NotificationDefinition AddDefinition(NotificationGroupDefinition group, string name)
    {
        return group.AddNotification(name, new FixedLocalizableString(name));
    }

    private static async Task StartHostAsync<TStartupModule>() where TStartupModule : IAbpModule
    {
        using var host = BuildHost<TStartupModule>();
        await host.StartAsync();
        await host.StopAsync();
    }

    private static IHost BuildHost<TStartupModule>() where TStartupModule : IAbpModule
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddApplication<TStartupModule>();
        // These tests exercise registration, not routing; give every definition a channel so the stateless-mode
        // startup check (no inbox store, no channel) does not apply.
        builder.Services.Configure<NotificationRoutingOptions>(options => options.Default = new[] { "Test" });
        return builder.Build();
    }
}

[NotificationDataType("Test.Duplicate")]
internal sealed class DuplicateDataA : NotificationData
{
}

[NotificationDataType("Test.Duplicate")]
internal sealed class DuplicateDataB : NotificationData
{
}

[NotificationDataType("Test.DistinctA")]
internal sealed class DistinctDataA : NotificationData
{
}

[NotificationDataType("Test.DistinctB")]
internal sealed class DistinctDataB : NotificationData
{
}

[NotificationDataType("Test.Case")]
internal sealed class CaseSensitiveDataUpper : NotificationData
{
}

[NotificationDataType("test.case")]
internal sealed class CaseSensitiveDataLower : NotificationData
{
}

internal sealed class ProviderDependencyDefinitionProvider : INotificationDefinitionProvider
{
    private readonly NotificationDefinitionRegistration _registration;
    private readonly INotificationDefinitionManager _definitionManager;

    public ProviderDependencyDefinitionProvider(
        IOptions<NotificationDefinitionRegistration> registration,
        INotificationDefinitionManager definitionManager)
    {
        _registration = registration.Value;
        _definitionManager = definitionManager;
    }

    public void Define(INotificationDefinitionContext context)
    {
        _registration.ShouldNotBeNull();
        _definitionManager.ShouldNotBeNull();
        _registration.DefinitionProviders.Count(type => type == typeof(ProviderDependencyDefinitionProvider)).ShouldBe(1);
        context.AddGroup("Test.ProviderDependencies").AddNotification(
            "Test.ProviderDependencies",
            new FixedLocalizableString("Provider dependencies"));
    }
}

[DisableConventionalRegistration]
internal sealed class CustomStaticNotificationDefinitionStore : StaticNotificationDefinitionStore
{
    public CustomStaticNotificationDefinitionStore(
        IOptions<NotificationDefinitionRegistration> registration,
        IServiceScopeFactory serviceScopeFactory)
        : base(registration, serviceScopeFactory)
    {
    }

    protected override IReadOnlyList<NotificationGroupDefinition> CreateGroups()
    {
        var context = new NotificationDefinitionContext();
        context.AddGroup("Test.CustomStore").AddNotification(
            "Test.CustomStore",
            new FixedLocalizableString("Custom store"));
        return context.Groups;
    }
}

internal sealed class DuplicateGroupDefinitionProviderA : INotificationDefinitionProvider
{
    public void Define(INotificationDefinitionContext context)
    {
        context.AddGroup("Test.DuplicateGroup");
    }
}

internal sealed class DuplicateGroupDefinitionProviderB : INotificationDefinitionProvider
{
    public void Define(INotificationDefinitionContext context)
    {
        context.AddGroup("Test.DuplicateGroup");
    }
}

internal sealed class GroupedDefinitionProvider : INotificationDefinitionProvider
{
    public void Define(INotificationDefinitionContext context)
    {
        context.AddGroup("Test.Orders")
            .AddNotification("Test.Order.Shipped", new FixedLocalizableString("Shipped"));
        context.AddGroup("Test.System")
            .AddNotification("Test.Announcement", new FixedLocalizableString("Announcement"));
        context.GetGroupOrNull("Test.Orders")!
            .AddNotification("Test.Order.Paid", new FixedLocalizableString("Paid"));
    }
}

[DependsOn(typeof(AbpNotificationsModule))]
public class DuplicateGroupsStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<DuplicateGroupDefinitionProviderA>();
        context.Services.AddTransient<DuplicateGroupDefinitionProviderB>();
        Configure<NotificationDefinitionRegistration>(options =>
        {
            options.DefinitionProviders.Add(typeof(DuplicateGroupDefinitionProviderA));
            options.DefinitionProviders.Add(typeof(DuplicateGroupDefinitionProviderB));
        });
    }
}

[DependsOn(typeof(AbpNotificationsModule))]
public class GroupedDefinitionsStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<GroupedDefinitionProvider>();
        Configure<NotificationDefinitionRegistration>(options =>
            options.DefinitionProviders.Add(typeof(GroupedDefinitionProvider)));
    }
}

[DependsOn(typeof(TestProviderAModule), typeof(TestProviderBModule))]
public class DuplicateDefinitionsForwardStartupModule : AbpModule
{
}

[DependsOn(typeof(TestProviderBModule), typeof(TestProviderAModule))]
public class DuplicateDefinitionsReverseStartupModule : AbpModule
{
}

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class DuplicateDataForwardStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDataOptions>(options =>
        {
            options.Add<DuplicateDataA>();
            options.Add<DuplicateDataB>();
        });
    }
}

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class DuplicateDataReverseStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDataOptions>(options =>
        {
            options.Add<DuplicateDataB>();
            options.Add<DuplicateDataA>();
        });
    }
}

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AmbiguousDataTypeStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDataOptions>(options =>
        {
            options.Add("Test.First", typeof(DistinctDataA));
            options.Add("Test.Second", typeof(DistinctDataA));
        });
    }
}

[DependsOn(typeof(AbpNotificationsModule))]
public class ValidRegistrationsStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDataOptions>(options =>
        {
            options.Add<DistinctDataA>();
            options.Add<DistinctDataA>();
            options.Add<DistinctDataB>();
        });
    }
}

[DependsOn(typeof(AbpNotificationsDistributionModule))]
public class InvalidDistributionBatchStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDistributionOptions>(options =>
            options.RecipientBatchSize = NotificationDistributionOptions.MaxBatchSize + 1);
    }
}

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AbstractionsOnlyStartupModule : AbpModule
{
}

[DependsOn(typeof(AbpNotificationsModule))]
public class ProviderDependencyStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<ProviderDependencyDefinitionProvider>();
        Configure<NotificationDefinitionRegistration>(options =>
            options.DefinitionProviders.Add(typeof(ProviderDependencyDefinitionProvider)));
    }
}

[DependsOn(typeof(TestProviderAModule))]
public class SingleDefinitionProviderStartupModule : AbpModule
{
}

[DependsOn(typeof(TestProviderAModule), typeof(TestProviderBModule))]
public class CustomStaticDefinitionStoreStartupModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(
            ServiceDescriptor.Singleton<IStaticNotificationDefinitionStore, CustomStaticNotificationDefinitionStore>());
    }
}
