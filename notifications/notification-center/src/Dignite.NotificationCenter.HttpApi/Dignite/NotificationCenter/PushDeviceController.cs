using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.NotificationCenter;

/// <summary>
/// REST endpoints for the current user's push devices, under <c>/api/notification-center/push-devices</c>. A thin
/// controller that delegates to <see cref="IPushDeviceAppService"/>, which owns authorization and per-user scoping.
/// Both actions are POSTs with the token in the body: a device token can be used to push to the device, so it is kept
/// out of URLs and therefore out of access logs.
/// </summary>
[RemoteService(Name = NotificationCenterRemoteServiceConsts.RemoteServiceName)]
[Area(NotificationCenterRemoteServiceConsts.ModuleName)]
[Route("api/notification-center/push-devices")]
public class PushDeviceController : NotificationCenterController, IPushDeviceAppService
{
    protected IPushDeviceAppService PushDeviceAppService { get; }

    public PushDeviceController(IPushDeviceAppService pushDeviceAppService)
    {
        PushDeviceAppService = pushDeviceAppService;
    }

    [HttpPost]
    [Route("register")]
    public virtual Task RegisterAsync([FromBody] PushDeviceInput input)
    {
        return PushDeviceAppService.RegisterAsync(input);
    }

    [HttpPost]
    [Route("unregister")]
    public virtual Task UnregisterAsync([FromBody] PushDeviceInput input)
    {
        return PushDeviceAppService.UnregisterAsync(input);
    }
}
