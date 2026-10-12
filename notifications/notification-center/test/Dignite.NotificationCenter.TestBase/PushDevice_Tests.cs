using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Dignite.Abp.Notifications.Push;
using Dignite.NotificationCenter.Push;
using Dignite.NotificationCenter.Push.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Provider-agnostic push device registry scenarios: registration through the app service, the bridge the push channel
/// reads devices through, and the optional Identity-session rule — run against both EF Core and MongoDB.
/// </summary>
public abstract class PushDevice_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Expo = "Expo";

    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();

    private Task RegisterAsync(Guid userId, string token, string culture = "en", string? sessionId = null)
    {
        return AsUserAsync(userId, sessionId, async () =>
        {
            using (CultureHelper.Use(culture))
            {
                await GetRequiredService<IPushDeviceAppService>()
                    .RegisterAsync(new PushDeviceInput { Provider = Expo, Token = token });
            }
        });
    }

    private Task UnregisterAsync(Guid userId, string token)
    {
        return AsUserAsync(userId, null, () => GetRequiredService<IPushDeviceAppService>()
            .UnregisterAsync(new PushDeviceInput { Provider = Expo, Token = token }));
    }

    private async Task AsUserAsync(Guid userId, string? sessionId, Func<Task> action)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        if (sessionId != null)
        {
            claims.Add(new Claim(AbpClaimTypes.SessionId, sessionId));
        }

        using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))))
        {
            await WithUnitOfWorkAsync(action);
        }
    }

    /// <summary>Every registration, in every tenant.</summary>
    private async Task<List<PushDevice>> GetAllDevicesAsync()
    {
        List<PushDevice> devices = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
            {
                devices = await GetRequiredService<IRepository<PushDevice, Guid>>().GetListAsync();
            }
        });
        return devices;
    }

    private async Task<IReadOnlyList<PushTarget>> GetTargetsAsync(IPushDeviceStore store, Guid userId)
    {
        IReadOnlyList<PushTarget> targets = null!;
        await WithUnitOfWorkAsync(async () => targets = await store.GetTargetsAsync(userId));
        return targets;
    }

    [Fact]
    public async Task Register_records_the_device_with_the_request_culture_and_the_login_session()
    {
        await RegisterAsync(_user1, "ExponentPushToken[a]", culture: "ja-JP", sessionId: "session-1");

        var device = (await GetAllDevicesAsync()).ShouldHaveSingleItem();
        device.UserId.ShouldBe(_user1);
        device.Provider.ShouldBe(Expo);
        device.Token.ShouldBe("ExponentPushToken[a]");
        device.TokenKey.ShouldBe(PushDeviceIdentity.GetTokenKey(Expo, "ExponentPushToken[a]"));
        device.CultureName.ShouldBe("ja-JP");
        device.SessionId.ShouldBe("session-1");
        device.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Registering_again_refreshes_the_device_instead_of_adding_one()
    {
        await RegisterAsync(_user1, "ExponentPushToken[a]", culture: "en");
        var first = (await GetAllDevicesAsync()).Single();

        await Task.Delay(20);
        await RegisterAsync(_user1, "ExponentPushToken[a]", culture: "fr-FR");

        var device = (await GetAllDevicesAsync()).ShouldHaveSingleItem();
        device.Id.ShouldBe(first.Id);
        device.CultureName.ShouldBe("fr-FR");
        device.LastSeenTime.ShouldBeGreaterThan(first.LastSeenTime);
        device.CreationTime.ShouldBe(first.CreationTime);
    }

    [Fact]
    public async Task A_device_registered_by_someone_else_moves_to_the_new_user()
    {
        await RegisterAsync(_user1, "ExponentPushToken[shared]");
        await RegisterAsync(_user2, "ExponentPushToken[shared]");

        var device = (await GetAllDevicesAsync()).ShouldHaveSingleItem();
        device.UserId.ShouldBe(_user2);
    }

    [Fact]
    public async Task A_device_moves_across_tenants_and_leaves_the_previous_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(tenantA))
        {
            await RegisterAsync(_user1, "ExponentPushToken[roaming]");
        }

        using (currentTenant.Change(tenantB))
        {
            await RegisterAsync(_user2, "ExponentPushToken[roaming]");
        }

        var device = (await GetAllDevicesAsync()).ShouldHaveSingleItem();
        device.TenantId.ShouldBe(tenantB);
        device.UserId.ShouldBe(_user2);

        var store = GetRequiredService<IPushDeviceStore>();
        using (currentTenant.Change(tenantA))
        {
            (await GetTargetsAsync(store, _user1)).ShouldBeEmpty();
        }

        using (currentTenant.Change(tenantB))
        {
            (await GetTargetsAsync(store, _user2)).ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task Unregister_removes_only_the_callers_own_device()
    {
        await RegisterAsync(_user1, "ExponentPushToken[mine]");

        await UnregisterAsync(_user2, "ExponentPushToken[mine]");
        (await GetAllDevicesAsync()).ShouldHaveSingleItem();

        await UnregisterAsync(_user1, "ExponentPushToken[unknown]");
        (await GetAllDevicesAsync()).ShouldHaveSingleItem();

        await UnregisterAsync(_user1, "ExponentPushToken[mine]");
        (await GetAllDevicesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Registering_beyond_the_limit_forgets_the_least_recently_seen_device()
    {
        GetRequiredService<IOptions<PushDeviceOptions>>().Value.MaxDevicesPerUser = 2;

        await RegisterAsync(_user1, "ExponentPushToken[old]");
        await Task.Delay(20);
        await RegisterAsync(_user1, "ExponentPushToken[middle]");
        await Task.Delay(20);
        await RegisterAsync(_user1, "ExponentPushToken[new]");

        (await GetAllDevicesAsync()).Select(device => device.Token).OrderBy(token => token)
            .ShouldBe(new[] { "ExponentPushToken[middle]", "ExponentPushToken[new]" });
    }

    [Fact]
    public async Task The_push_channel_reads_devices_through_the_registry_and_forgets_dead_ones()
    {
        var store = GetRequiredService<IPushDeviceStore>();
        store.ShouldBeOfType<NotificationCenterPushDeviceStore>();

        await RegisterAsync(_user1, "ExponentPushToken[a]", culture: "ja-JP");
        await RegisterAsync(_user2, "ExponentPushToken[other]");

        var target = (await GetTargetsAsync(store, _user1)).ShouldHaveSingleItem();
        target.Provider.ShouldBe(Expo);
        target.Token.ShouldBe("ExponentPushToken[a]");
        target.CultureName.ShouldBe("ja-JP");

        await WithUnitOfWorkAsync(() => store.RemoveAsync("EXPO", "ExponentPushToken[a]"));

        (await GetTargetsAsync(store, _user1)).ShouldBeEmpty();
        (await GetTargetsAsync(store, _user2)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_delivered_notification_reaches_the_registered_device_and_a_dead_device_is_forgotten()
    {
        await RegisterAsync(_user1, "ExponentPushToken[alive]");
        await RegisterAsync(_user1, "ExponentPushToken[dead]");
        var provider = new RecordingProvider(token => token == "ExponentPushToken[dead]");
        var serializer = GetRequiredService<INotificationDataSerializer>();
        var notifier = new PushNotifier(
            GetRequiredService<IPushDeviceStore>(),
            new IPushProvider[] { provider },
            GetRequiredService<INotificationPushBuilder>(),
            serializer,
            NullLogger<PushNotifier>.Instance,
            Options.Create(new NotificationPushOptions()));
        var notificationId = Guid.NewGuid();

        await WithUnitOfWorkAsync(() => notifier.DeliverAsync(new NotificationDeliveryRequestedEto
        {
            NotificationId = notificationId,
            NotificationName = TestNotificationDefinitionProvider.OrderShipped,
            DataJson = serializer.Serialize(new MessageNotificationData("Your order shipped")),
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserId = _user1,
            Channel = PushNotifier.ChannelName
        }));

        provider.Sent.Select(message => message.Token).OrderBy(token => token)
            .ShouldBe(new[] { "ExponentPushToken[alive]", "ExponentPushToken[dead]" });
        provider.Sent.ShouldAllBe(message => message.Body == "Your order shipped");
        provider.Sent[0].Data[PushDataKeys.NotificationId].ShouldBe(notificationId.ToString());
        (await GetAllDevicesAsync()).Select(device => device.Token)
            .ShouldBe(new[] { "ExponentPushToken[alive]" });
    }

    [Fact]
    public async Task With_identity_sessions_a_device_whose_session_ended_is_forgotten()
    {
        await RegisterAsync(_user1, "ExponentPushToken[live-session]", sessionId: "live");
        await RegisterAsync(_user1, "ExponentPushToken[ended-session]", sessionId: "ended");
        await RegisterAsync(_user1, "ExponentPushToken[no-session]");
        var sessions = Substitute.For<IIdentitySessionRepository>();
        sessions.GetListAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Is<Guid?>(id => id == _user1),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<IdentitySession>
            {
                new(Guid.NewGuid(), "live", "Mobile", "test", _user1, null, "app", "127.0.0.1", DateTime.UtcNow)
            });
        var store = new IdentitySessionPushDeviceStore(GetRequiredService<PushDeviceManager>(), sessions);

        var targets = await GetTargetsAsync(store, _user1);

        targets.Select(target => target.Token).OrderBy(token => token).ShouldBe(new[]
        {
            "ExponentPushToken[live-session]",
            "ExponentPushToken[no-session]"
        });
        (await GetAllDevicesAsync()).ShouldNotContain(device => device.Token == "ExponentPushToken[ended-session]");
        // One session query for all of the user's devices, not one per device.
        await sessions.Received(1).GetListAsync(
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sessions.DidNotReceiveWithAnyArgs().ExistAsync(default(string)!, default);
    }

    [Fact]
    public async Task A_registration_that_loses_a_race_for_a_new_token_is_retried_and_refreshes_the_winner()
    {
        var manager = new RacingPushDeviceManager(this, competitorUserId: _user2);
        var appService = new PushDeviceAppService(manager)
        {
            LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
        };

        using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
                   new[] { new Claim(AbpClaimTypes.UserId, _user1.ToString()) }, "Test"))))
        {
            await appService.RegisterAsync(new PushDeviceInput { Provider = Expo, Token = "ExponentPushToken[raced]" });
        }

        manager.Attempts.ShouldBe(2);
        var device = (await GetAllDevicesAsync()).ShouldHaveSingleItem();
        device.UserId.ShouldBe(_user1);
    }

    /// <summary>
    /// Stands in for two concurrent first registrations of one token: on the first attempt a competing request
    /// commits the row and this one fails, as an insert hitting the unique token index would.
    /// </summary>
    private sealed class RacingPushDeviceManager : PushDeviceManager
    {
        private readonly PushDevice_Tests<TStartupModule> _test;
        private readonly Guid _competitorUserId;

        public int Attempts { get; private set; }

        public RacingPushDeviceManager(PushDevice_Tests<TStartupModule> test, Guid competitorUserId)
            : base(
                test.GetRequiredService<IRepository<PushDevice, Guid>>(),
                test.GetRequiredService<IDataFilter>(),
                test.GetRequiredService<ICurrentTenant>(),
                test.GetRequiredService<IGuidGenerator>(),
                test.GetRequiredService<IClock>(),
                test.GetRequiredService<IOptions<PushDeviceOptions>>())
        {
            _test = test;
            _competitorUserId = competitorUserId;
        }

        public override async Task<PushDevice> RegisterAsync(
            Guid userId,
            string provider,
            string token,
            string? cultureName,
            string? sessionId,
            CancellationToken cancellationToken = default)
        {
            if (++Attempts == 1)
            {
                using (var competitor = _test.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
                {
                    await base.RegisterAsync(_competitorUserId, provider, token, cultureName, null, cancellationToken);
                    await competitor.CompleteAsync();
                }

                throw new InvalidOperationException("Simulated unique token index violation.");
            }

            return await base.RegisterAsync(userId, provider, token, cultureName, sessionId, cancellationToken);
        }
    }

    private sealed class RecordingProvider : IPushProvider
    {
        private readonly Func<string, bool> _isDead;

        public string Name => Expo;

        public List<PushMessage> Sent { get; } = new();

        public RecordingProvider(Func<string, bool> isDead)
        {
            _isDead = isDead;
        }

        public Task<IReadOnlyList<PushSendResult>> SendAsync(
            IReadOnlyList<PushMessage> messages,
            CancellationToken cancellationToken = default)
        {
            Sent.AddRange(messages);
            return Task.FromResult<IReadOnlyList<PushSendResult>>(messages
                .Select(message => _isDead(message.Token)
                    ? PushSendResult.TokenInvalid(message.Token, "DeviceNotRegistered")
                    : PushSendResult.Succeeded(message.Token))
                .ToList());
        }
    }
}
