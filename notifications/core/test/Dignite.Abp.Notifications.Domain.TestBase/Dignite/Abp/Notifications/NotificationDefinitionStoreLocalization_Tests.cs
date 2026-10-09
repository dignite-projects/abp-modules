using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The notification service localizes a publisher's display texts by resource name: the resource is not registered in
/// this process, so ABP finds it in the external localization store (Language Management in a microservice solution).
/// Provider-agnostic: each persistence provider's test project runs it on its own database.
/// </summary>
public abstract class NotificationDefinitionStoreLocalization_Tests<TInfrastructure>
    where TInfrastructure : SharedDefinitionStoreInfrastructure, new()
{
    [Fact]
    public async Task Display_texts_saved_by_name_resolve_through_the_external_localization_store()
    {
        using var shared = new TInfrastructure();
        await using (var publisherA = await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);
        var manager = notificationService.Get<INotificationDefinitionManager>();
        var localizerFactory = notificationService.Get<IStringLocalizerFactory>();

        var definition = await manager.GetAsync(PublisherADefinitionProvider.OrderShipped);
        var group = (await manager.GetGroupOrNullAsync(definition.GroupName))!;

        using (CultureHelper.Use("en"))
        {
            definition.DisplayName.Localize(localizerFactory).Value.ShouldBe("Your order has shipped");
            definition.Description!.Localize(localizerFactory).Value.ShouldBe("Sent when an order leaves the warehouse");
            group.DisplayName.Localize(localizerFactory).Value.ShouldBe("Orders");
        }

        // A fixed text is stored as it is.
        (await manager.GetAsync(PublisherADefinitionProvider.OrderCancelled)).DisplayName.Localize(localizerFactory).Value
            .ShouldBe("Order cancelled");
    }
}
