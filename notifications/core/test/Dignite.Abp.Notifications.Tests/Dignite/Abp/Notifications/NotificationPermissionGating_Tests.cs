using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Verifies permission-gated availability works through <see cref="INotificationPermissionChecker"/>, which the
/// singleton definition manager resolves from a fresh scope per call (roadmap B). The Identity integration plugs a
/// real ABP authorization check into this same seam.
/// </summary>
public class NotificationPermissionGating_Tests : DigniteAbpNotificationsTestBase
{
    private readonly INotificationDefinitionManager _definitionManager;

    public NotificationPermissionGating_Tests()
    {
        _definitionManager = GetRequiredService<INotificationDefinitionManager>();
    }

    [Fact]
    public async Task Availability_reflects_permission_gating()
    {
        var userId = Guid.NewGuid();

        (await _definitionManager.IsAvailableAsync(TestNotificationDefinitionProvider.PermissionGranted, userId))
            .ShouldBeTrue();
        (await _definitionManager.IsAvailableAsync(TestNotificationDefinitionProvider.PermissionDenied, userId))
            .ShouldBeFalse();

        var availableNames = (await _definitionManager.GetAllAvailableAsync(userId)).Select(d => d.Name).ToList();
        availableNames.ShouldContain(TestNotificationDefinitionProvider.PermissionGranted);
        availableNames.ShouldNotContain(TestNotificationDefinitionProvider.PermissionDenied);
    }
}
