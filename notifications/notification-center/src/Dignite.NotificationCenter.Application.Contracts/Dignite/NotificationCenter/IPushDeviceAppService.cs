using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.NotificationCenter;

/// <summary>
/// The current user's push devices. The app registers on every launch, after sign-in and when its language changes,
/// and unregisters at sign-out — before revoking its access token, since the call needs it.
/// </summary>
public interface IPushDeviceAppService : IApplicationService
{
    /// <summary>
    /// Registers the device for the current user, or refreshes it. The device's language is the request culture
    /// (the app's <c>Accept-Language</c>) and its login session the caller's <c>session_id</c> claim, if any. A device
    /// registered to someone else moves to the current user.
    /// </summary>
    Task RegisterAsync(PushDeviceInput input);

    /// <summary>Forgets the device if it is the current user's; otherwise does nothing.</summary>
    Task UnregisterAsync(PushDeviceInput input);
}
