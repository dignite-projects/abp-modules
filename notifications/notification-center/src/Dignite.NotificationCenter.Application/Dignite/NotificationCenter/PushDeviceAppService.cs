using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Users;

namespace Dignite.NotificationCenter;

[Authorize]
public class PushDeviceAppService : NotificationCenterAppService, IPushDeviceAppService
{
    protected PushDeviceManager PushDeviceManager { get; }

    public PushDeviceAppService(PushDeviceManager pushDeviceManager)
    {
        PushDeviceManager = pushDeviceManager;
    }

    public virtual async Task RegisterAsync(PushDeviceInput input)
    {
        await PushDeviceManager.RegisterAsync(
            CurrentUser.GetId(),
            input.Provider.Trim(),
            input.Token.Trim(),
            CultureInfo.CurrentUICulture.Name,
            CurrentUser.FindSessionId());
    }

    public virtual async Task UnregisterAsync(PushDeviceInput input)
    {
        await PushDeviceManager.UnregisterAsync(CurrentUser.GetId(), input.Provider.Trim(), input.Token.Trim());
    }
}
