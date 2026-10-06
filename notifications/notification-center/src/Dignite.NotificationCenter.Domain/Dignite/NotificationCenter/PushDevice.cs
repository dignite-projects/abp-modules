using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter;

/// <summary>
/// A phone (more exactly: one installation of an app) a user can be pushed to, identified by the token its push
/// provider issued. The token belongs to the device, not to a user or tenant: when someone else signs in on the same
/// device, the registration moves to them (see <see cref="PushDeviceManager"/>).
/// </summary>
public class PushDevice : BasicAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid UserId { get; protected set; }

    /// <summary>The push provider that issued <see cref="Token"/>, e.g. <c>"Expo"</c>.</summary>
    public virtual string Provider { get; protected set; } = default!;

    /// <summary>The provider-issued device token. Treat it as a secret: never log it or put it in a URL.</summary>
    public virtual string Token { get; protected set; } = default!;

    /// <summary>Unique key of <see cref="Provider"/> + <see cref="Token"/>. See <see cref="PushDeviceIdentity"/>.</summary>
    public virtual string TokenKey { get; protected set; } = default!;

    /// <summary>The app's language when it last registered; push content is built in it.</summary>
    public virtual string? CultureName { get; protected set; }

    /// <summary>
    /// The ABP login session (<c>session_id</c> claim) the device last registered under, when the host issues one.
    /// Lets a host that tracks sessions stop pushing to a device whose session has ended.
    /// </summary>
    public virtual string? SessionId { get; protected set; }

    public virtual DateTime CreationTime { get; protected set; }

    /// <summary>When the app last registered the device. Apps register on every launch.</summary>
    public virtual DateTime LastSeenTime { get; protected set; }

    protected PushDevice()
    {
    }

    public PushDevice(
        Guid id,
        Guid userId,
        string provider,
        string token,
        string? cultureName,
        string? sessionId,
        DateTime now,
        Guid? tenantId)
        : base(id)
    {
        Provider = Check.NotNullOrWhiteSpace(provider, nameof(provider), PushDeviceConsts.MaxProviderLength);
        Token = Check.NotNullOrWhiteSpace(token, nameof(token), PushDeviceConsts.MaxTokenLength);
        TokenKey = PushDeviceIdentity.GetTokenKey(provider, token);
        CreationTime = now;
        Bind(userId, cultureName, sessionId, now, tenantId);
    }

    /// <summary>
    /// Records a registration: the device now belongs to <paramref name="userId"/> in <paramref name="tenantId"/>,
    /// with the app's current language and login session.
    /// </summary>
    public virtual void Bind(Guid userId, string? cultureName, string? sessionId, DateTime now, Guid? tenantId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A push device user identifier cannot be Guid.Empty.", nameof(userId));
        }

        UserId = userId;
        TenantId = tenantId;
        CultureName = string.IsNullOrWhiteSpace(cultureName)
            ? null
            : Check.Length(cultureName, nameof(cultureName), PushDeviceConsts.MaxCultureNameLength);
        SessionId = string.IsNullOrWhiteSpace(sessionId)
            ? null
            : Check.Length(sessionId, nameof(sessionId), PushDeviceConsts.MaxSessionIdLength);
        LastSeenTime = now;
    }
}
