using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Dignite.NotificationCenter;

/// <summary>
/// The push device registry: registration, sign-out, and forgetting dead devices. Every device query goes through
/// here, against the generic repository — there is no custom repository interface, and push devices are not a Core
/// concept, so they stay off <c>INotificationStore</c>.
/// </summary>
public class PushDeviceManager : ITransientDependency
{
    protected IRepository<PushDevice, Guid> Repository { get; }

    protected IDataFilter DataFilter { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected IClock Clock { get; }

    protected PushDeviceOptions Options { get; }

    public PushDeviceManager(
        IRepository<PushDevice, Guid> repository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IClock clock,
        IOptions<PushDeviceOptions> options)
    {
        Repository = repository;
        DataFilter = dataFilter;
        CurrentTenant = currentTenant;
        GuidGenerator = guidGenerator;
        Clock = clock;
        Options = options.Value;
    }

    /// <summary>
    /// Registers the device for the user in the current tenant, or refreshes it. A device already registered to
    /// someone else — in any tenant — moves to this user, so whoever signed in last on a phone is the one it gets
    /// pushes for.
    /// </summary>
    /// <remarks>
    /// The lookup deliberately disables the tenant filter: a token identifies a physical device, which belongs to no
    /// tenant, and only a globally unique registration guarantees the previous tenant stops pushing to it. This is the
    /// one place the registry crosses tenants. A host with a database per tenant cannot see across databases, so there
    /// the guarantee rests on the app unregistering at sign-out and on dead-device reports.
    /// </remarks>
    public virtual async Task<PushDevice> RegisterAsync(
        Guid userId,
        string provider,
        string token,
        string? cultureName,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var tokenKey = PushDeviceIdentity.GetTokenKey(provider, token);
        var now = Clock.Now;
        PushDevice device;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var existing = await Repository.FindAsync(
                d => d.TokenKey == tokenKey,
                cancellationToken: cancellationToken);

            if (existing == null)
            {
                device = await Repository.InsertAsync(
                    new PushDevice(
                        GuidGenerator.Create(),
                        userId,
                        provider,
                        token,
                        cultureName,
                        sessionId,
                        now,
                        CurrentTenant.Id),
                    autoSave: true,
                    cancellationToken: cancellationToken);
            }
            else
            {
                existing.Bind(userId, cultureName, sessionId, now, CurrentTenant.Id);
                device = await Repository.UpdateAsync(existing, autoSave: true, cancellationToken: cancellationToken);
            }
        }

        await TrimAsync(userId, cancellationToken);
        return device;
    }

    /// <summary>
    /// Forgets the device if it is registered to this user in the current tenant. Anything else is silently ignored,
    /// so the call cannot be used to learn whether a token is registered or to remove someone else's device.
    /// </summary>
    public virtual async Task UnregisterAsync(
        Guid userId,
        string provider,
        string token,
        CancellationToken cancellationToken = default)
    {
        var device = await FindAsync(provider, token, cancellationToken);
        if (device != null && device.UserId == userId)
        {
            await Repository.DeleteAsync(device, autoSave: true, cancellationToken: cancellationToken);
        }
    }

    /// <summary>The user's devices in the current tenant, most recently seen first.</summary>
    public virtual async Task<List<PushDevice>> GetListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var devices = await Repository.GetListAsync(d => d.UserId == userId, cancellationToken: cancellationToken);
        return devices.OrderByDescending(d => d.LastSeenTime).ToList();
    }

    /// <summary>Forgets a device a push provider reported dead. An unknown device is not an error.</summary>
    public virtual async Task RemoveAsync(string provider, string token, CancellationToken cancellationToken = default)
    {
        var device = await FindAsync(provider, token, cancellationToken);
        if (device != null)
        {
            await Repository.DeleteAsync(device, autoSave: true, cancellationToken: cancellationToken);
        }
    }

    public virtual Task RemoveAsync(PushDevice device, CancellationToken cancellationToken = default)
    {
        return Repository.DeleteAsync(device, autoSave: true, cancellationToken: cancellationToken);
    }

    protected virtual Task<PushDevice?> FindAsync(string provider, string token, CancellationToken cancellationToken)
    {
        var tokenKey = PushDeviceIdentity.GetTokenKey(provider, token);
        return Repository.FindAsync(d => d.TokenKey == tokenKey, cancellationToken: cancellationToken);
    }

    /// <summary>Keeps at most <see cref="PushDeviceOptions.MaxDevicesPerUser"/>, dropping the least recently seen.</summary>
    protected virtual async Task TrimAsync(Guid userId, CancellationToken cancellationToken)
    {
        var max = Math.Max(1, Options.MaxDevicesPerUser);
        var devices = await GetListAsync(userId, cancellationToken);
        if (devices.Count <= max)
        {
            return;
        }

        await Repository.DeleteManyAsync(devices.Skip(max), autoSave: true, cancellationToken: cancellationToken);
    }
}
