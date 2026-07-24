using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Dignite.NotificationCenter.Web.Host.Notifications;

/// <summary>
/// Seeds a second "demo" user (password "1q2w3E*") and, on first run, publishes a few notifications to both
/// admin (seeded by ABP's IdentityDataSeedContributor) and demo, so the bell isn't empty and per-user inbox
/// differences are visible. Runs via ABP's IDataSeeder (invoked by HostDbMigrationService) inside a proper UoW.
/// Idempotent: the presence of the "demo" user means we've already run.
/// </summary>
public class DemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string DemoPassword = "1q2w3E*";

    protected IdentityUserManager UserManager { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected INotificationPublisher Publisher { get; }

    public DemoDataSeedContributor(
        IdentityUserManager userManager,
        IGuidGenerator guidGenerator,
        INotificationPublisher publisher)
    {
        UserManager = userManager;
        GuidGenerator = guidGenerator;
        Publisher = publisher;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await UserManager.FindByNameAsync("demo") != null)
        {
            return; // already seeded
        }

        var demo = new IdentityUser(GuidGenerator.Create(), "demo", "demo@demo.local");
        (await UserManager.CreateAsync(demo, DemoPassword)).CheckErrors();

        var admin = await UserManager.FindByNameAsync("admin");
        var recipients = admin != null ? new[] { admin.Id, demo.Id } : new[] { demo.Id };

        await Publisher.PublishAsync(
            "Demo.OrderShipped",
            new OrderShippedNotificationData
            {
                OrderNumber = "SO-1001",
                ItemCount = 3,
                ImageUrl = "https://placehold.co/60x60"
            },
            new NotificationEntityIdentifier("Demo.Order", "1001"),
            NotificationSeverity.Success,
            recipients);

        await Publisher.PublishAsync(
            "Demo.Announcement",
            new MessageNotificationData("Welcome to the Notification Center demo!"),
            severity: NotificationSeverity.Info,
            userIds: recipients);
    }
}
