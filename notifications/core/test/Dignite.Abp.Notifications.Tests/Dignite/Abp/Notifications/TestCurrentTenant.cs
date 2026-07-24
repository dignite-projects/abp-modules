using System;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications;

internal class TestCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => Id.HasValue;

    public Guid? Id { get; private set; }

    public string? Name { get; private set; }

    public IDisposable Change(Guid? id, string? name = null)
    {
        var previousId = Id;
        var previousName = Name;
        Id = id;
        Name = name;
        return new DisposeAction(() =>
        {
            Id = previousId;
            Name = previousName;
        });
    }
}
