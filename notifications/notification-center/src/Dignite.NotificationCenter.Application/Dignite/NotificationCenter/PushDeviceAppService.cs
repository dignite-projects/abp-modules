using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Uow;
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

    /// <remarks>
    /// Apps register from several places at once (launch, sign-in, the token listener), so two requests can both miss
    /// a new token and both insert it; the loser hits the unique token index. Each attempt runs in its own unit of
    /// work, and a failed one is retried once: by then the winner's row exists and is simply refreshed. Any other
    /// failure fails the retry the same way and surfaces.
    /// </remarks>
    [UnitOfWork(IsDisabled = true)]
    public virtual async Task RegisterAsync(PushDeviceInput input)
    {
        var userId = CurrentUser.GetId();
        var provider = input.Provider.Trim();
        var token = input.Token.Trim();
        var cultureName = CultureInfo.CurrentUICulture.Name;
        var sessionId = CurrentUser.FindSessionId();

        try
        {
            await RegisterInNewUnitOfWorkAsync(userId, provider, token, cultureName, sessionId);
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            await RegisterInNewUnitOfWorkAsync(userId, provider, token, cultureName, sessionId);
        }
    }

    protected virtual async Task RegisterInNewUnitOfWorkAsync(
        Guid userId,
        string provider,
        string token,
        string cultureName,
        string? sessionId)
    {
        using var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        await PushDeviceManager.RegisterAsync(userId, provider, token, cultureName, sessionId);
        await unitOfWork.CompleteAsync();
    }

    public virtual async Task UnregisterAsync(PushDeviceInput input)
    {
        await PushDeviceManager.UnregisterAsync(CurrentUser.GetId(), input.Provider.Trim(), input.Token.Trim());
    }
}
