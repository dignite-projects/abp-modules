using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.PermissionManagement;

namespace Dignite.Abp.BlobManagement.Authorization;

/// <summary>
/// Deletes the resource grants of a directory when it is deleted, in the same unit of work (ABP publishes local entity
/// events before the transaction commits), so a grant never outlives its directory.
/// <para>
/// The grants live in ABP's Permission Management, which Blob Management does not require: the BLOB path needs no
/// permission data, so a service that only serves applications runs without it. Where the host has no grant store,
/// there is nothing to delete.
/// </para>
/// </summary>
public class BlobManagementResourcePermissionCleaner :
    ILocalEventHandler<EntityDeletedEventData<DirectoryDescriptor>>,
    ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    public BlobManagementResourcePermissionCleaner(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public virtual async Task HandleEventAsync(EntityDeletedEventData<DirectoryDescriptor> eventData)
    {
        var repository = ServiceProvider.GetService<IResourcePermissionGrantRepository>();
        if (repository == null)
        {
            return;
        }

        var grants = await repository.GetPermissionsAsync(BlobManagementResourceNames.Directory, eventData.Entity.Id.ToString());
        if (grants.Count > 0)
        {
            await repository.DeleteManyAsync(grants);
        }
    }
}
