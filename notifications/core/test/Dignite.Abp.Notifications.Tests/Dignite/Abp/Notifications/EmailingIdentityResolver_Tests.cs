using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Dignite.Abp.Notifications.Emailing.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.Notifications;

public class EmailingIdentityResolver_Tests
{
    [Fact]
    public async Task Resolves_email_from_identity_user()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<IIdentityUserRepository>();
        repository.FindAsync(userId).Returns(new IdentityUser(userId, "test-user", "test@example.com"));

        var resolver = new IdentityEmailNotificationAddressResolver(repository);

        (await resolver.GetEmailOrNullAsync(CreateContext(userId)))!.Address.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task Returns_null_when_identity_user_does_not_exist()
    {
        var repository = Substitute.For<IIdentityUserRepository>();
        repository.FindAsync(Arg.Any<Guid>()).Returns((IdentityUser?)null);

        var resolver = new IdentityEmailNotificationAddressResolver(repository);

        (await resolver.GetEmailOrNullAsync(CreateContext(Guid.NewGuid()))).ShouldBeNull();
    }

    [Fact]
    public async Task Returns_null_when_identity_user_has_no_email()
    {
        var userId = Guid.NewGuid();
        var user = new IdentityUser(userId, "test-user", "test@example.com");
        user.SetEmailWithoutValidation(string.Empty, string.Empty);

        var repository = Substitute.For<IIdentityUserRepository>();
        repository.FindAsync(userId).Returns(user);

        var resolver = new IdentityEmailNotificationAddressResolver(repository);

        (await resolver.GetEmailOrNullAsync(CreateContext(userId))).ShouldBeNull();
    }

    [Fact]
    public void Identity_resolver_is_the_built_in_fallback()
    {
        new IdentityEmailNotificationAddressResolver(Substitute.For<IIdentityUserRepository>())
            .Order.ShouldBe(NotificationEmailProviderOrders.BuiltInFallback);

        NotificationEmailProviderOrders.BuiltInFallback
            .ShouldBeGreaterThan(NotificationEmailProviderOrders.Default);
    }

    [Fact]
    public async Task Resolves_the_recipient_culture_from_the_user_setting()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<IIdentityUserRepository>();
        repository.FindAsync(userId).Returns(new IdentityUser(userId, "test-user", "test@example.com"));
        var settingManager = Substitute.For<ISettingManager>();
        settingManager.GetOrNullAsync(
                LocalizationSettingNames.DefaultLanguage,
                "U",
                userId.ToString(),
                true)
            .Returns("zh-Hans");

        var resolver = new IdentityEmailNotificationAddressResolver(repository, settingManager);

        var address = await resolver.GetEmailOrNullAsync(CreateContext(userId));

        address.ShouldNotBeNull();
        address!.Address.ShouldBe("test@example.com");
        address.CultureName.ShouldBe("zh-Hans");
    }

    [Fact]
    public async Task Uses_the_setting_management_fallback_when_no_user_culture_exists()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = Substitute.For<IIdentityUserRepository>();
        repository.FindAsync(userId).Returns(new IdentityUser(userId, "test-user", "test@example.com"));
        var settingManager = Substitute.For<ISettingManager>();
        settingManager.GetOrNullAsync(
                LocalizationSettingNames.DefaultLanguage,
                "U",
                userId.ToString(),
                true)
            .Returns("en-GB");

        var resolver = new IdentityEmailNotificationAddressResolver(repository, settingManager);

        var address = await resolver.GetEmailOrNullAsync(CreateContext(userId, tenantId));

        address.ShouldNotBeNull();
        address!.CultureName.ShouldBe("en-GB");
    }

    internal static EmailNotificationAddressResolveContext CreateContext(
        Guid userId, Guid? tenantId = null, string? entityTypeName = null, string? entityId = null)
    {
        return new EmailNotificationAddressResolveContext(
            new NotificationPayload
            {
                NotificationId = Guid.NewGuid(),
                NotificationName = "test.notification",
                Data = new MessageNotificationData("test"),
                Severity = NotificationSeverity.Info,
                CreationTime = DateTime.UtcNow,
                EntityTypeName = entityTypeName,
                EntityId = entityId
            },
            userId,
            tenantId);
    }
}

public class EmailingIdentityDependency_Tests : AbpIntegratedTest<EmailingIdentityDependencyTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public void Emailing_identity_module_contributes_the_built_in_fallback_resolver()
    {
        // It no longer replaces IEmailNotificationAddressResolver; it joins the chain, so an application resolver
        // coexists with it rather than having to displace it.
        GetRequiredService<IEnumerable<IEmailNotificationAddressResolver>>()
            .OfType<IdentityEmailNotificationAddressResolver>()
            .ShouldHaveSingleItem();
    }
}

[DependsOn(
    typeof(AbpNotificationsEmailingIdentityModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class EmailingIdentityDependencyTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(Substitute.For<IIdentityUserRepository>());
    }
}
