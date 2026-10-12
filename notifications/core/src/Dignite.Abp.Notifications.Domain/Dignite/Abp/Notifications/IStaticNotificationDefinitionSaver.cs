using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>Saves this process's static definitions to the store, as ABP's <c>IStaticPermissionSaver</c>.</summary>
public interface IStaticNotificationDefinitionSaver
{
    Task SaveAsync();
}
