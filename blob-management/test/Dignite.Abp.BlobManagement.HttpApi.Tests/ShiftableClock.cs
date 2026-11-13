using System;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Dignite.Abp.BlobManagement;

/// <summary>ABP's clock, moved forward by <see cref="Offset"/>: lets a test see a ticket expire without waiting.</summary>
[Dependency(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton, ReplaceServices = true)]
[ExposeServices(typeof(IClock), typeof(ShiftableClock))]
public class ShiftableClock : Clock
{
    public ShiftableClock(IOptions<AbpClockOptions> options, ICurrentTimezoneProvider currentTimezoneProvider, ITimezoneProvider timezoneProvider)
        : base(options, currentTimezoneProvider, timezoneProvider)
    {
    }

    public TimeSpan Offset { get; set; }

    public override DateTime Now => base.Now + Offset;
}
