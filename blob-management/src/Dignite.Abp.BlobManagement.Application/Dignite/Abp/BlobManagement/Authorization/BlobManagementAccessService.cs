using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.BlobManagement.Authorization;

/// <summary>
/// Decides what a user may do with the directories of a container: the one place Blob Management's authorization of
/// users is worked out, for the application services.
/// <para>
/// The caller sees a container's directory tree with the management permission, or with the view permission granted
/// on the container; and creates, renames and moves, or deletes directories there with the module-wide permission for
/// it, or with the one granted on the container. The answer is the container's: no directory has permissions of its
/// own, so nothing is asked for a directory, let alone for a file.
/// </para>
/// <para>
/// ABP's resource-permission checker answers one question: is this permission granted on this one resource. It is
/// asked here once per container and request, after the module-wide permissions are read once.
/// </para>
/// <para>
/// Only users get access: a principal without a user id - a client-credentials token - is allowed nothing. A client
/// keeps files, through the BLOB API and the file API, and those authorize it by the container's allow-list.
/// </para>
/// </summary>
public class BlobManagementAccessService : ITransientDependency
{
    protected static readonly string[] ModuleWidePermissions =
    {
        BlobManagementPermissions.Containers.Default,
        BlobManagementPermissions.Containers.CreateDirectories,
        BlobManagementPermissions.Containers.UpdateDirectories,
        BlobManagementPermissions.Containers.DeleteDirectories
    };

    protected static readonly string[] ContainerPermissions =
    {
        BlobManagementPermissions.Container.View,
        BlobManagementPermissions.Container.CreateDirectories,
        BlobManagementPermissions.Container.UpdateDirectories,
        BlobManagementPermissions.Container.DeleteDirectories
    };

    protected IPermissionChecker PermissionChecker { get; }

    protected IResourcePermissionChecker ResourcePermissionChecker { get; }

    protected ICurrentPrincipalAccessor PrincipalAccessor { get; }

    public BlobManagementAccessService(
        IPermissionChecker permissionChecker,
        IResourcePermissionChecker resourcePermissionChecker,
        ICurrentPrincipalAccessor principalAccessor)
    {
        PermissionChecker = permissionChecker;
        ResourcePermissionChecker = resourcePermissionChecker;
        PrincipalAccessor = principalAccessor;
    }

    /// <summary>
    /// Starts answering for the current principal: reads the module-wide permissions, once. The context is then asked
    /// about any container.
    /// </summary>
    public virtual async Task<BlobManagementAccessContext> BeginAsync()
    {
        var principal = PrincipalAccessor.Principal;
        var context = new BlobManagementAccessContext(principal, principal?.FindUserId());
        if (context.UserId == null)
        {
            return context;
        }

        var result = await PermissionChecker.IsGrantedAsync(principal, ModuleWidePermissions);
        foreach (var permission in result.Result.Where(r => r.Value == PermissionGrantResult.Granted))
        {
            context.ModuleWide.Add(permission.Key);
        }

        return context;
    }

    /// <summary>
    /// Whether the caller has anything to do in <paramref name="containerName"/>: the management permission, or any
    /// grant on the container.
    /// </summary>
    public virtual async Task<bool> HasContainerAsync(BlobManagementAccessContext context, string containerName)
    {
        if (context.UserId == null)
        {
            return false;
        }

        return context.ModuleWide.Contains(BlobManagementPermissions.Containers.Default) ||
               (await GetContainerGrantsAsync(context, containerName)).Count > 0;
    }

    /// <summary>What the caller may do with the directories of <paramref name="containerName"/>.</summary>
    public virtual async Task<BlobManagementAccess> GetAccessAsync(BlobManagementAccessContext context, string containerName)
    {
        if (context.UserId == null)
        {
            return BlobManagementAccess.None;
        }

        var moduleWide = context.ModuleWide;
        var container = await GetContainerGrantsAsync(context, containerName);

        return new BlobManagementAccess(
            view: moduleWide.Contains(BlobManagementPermissions.Containers.Default) ||
                  container.Contains(BlobManagementPermissions.Container.View),
            createDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.CreateDirectories) ||
                               container.Contains(BlobManagementPermissions.Container.CreateDirectories),
            updateDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.UpdateDirectories) ||
                               container.Contains(BlobManagementPermissions.Container.UpdateDirectories),
            deleteDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.DeleteDirectories) ||
                               container.Contains(BlobManagementPermissions.Container.DeleteDirectories));
    }

    /// <summary>The permissions granted on one container, asked once per context.</summary>
    protected virtual async Task<HashSet<string>> GetContainerGrantsAsync(BlobManagementAccessContext context, string containerName)
    {
        if (context.Grants.TryGetValue(containerName, out var granted))
        {
            return granted;
        }

        var result = await ResourcePermissionChecker.IsGrantedAsync(
            context.Principal, ContainerPermissions, BlobManagementResourceNames.Container, containerName);
        granted = new HashSet<string>(
            result.Result.Where(r => r.Value == PermissionGrantResult.Granted).Select(r => r.Key),
            StringComparer.Ordinal);
        context.Grants[containerName] = granted;
        return granted;
    }
}

/// <summary>What one request has read so far about its caller, so nothing is read twice.</summary>
public class BlobManagementAccessContext
{
    public BlobManagementAccessContext(ClaimsPrincipal? principal, Guid? userId)
    {
        Principal = principal;
        UserId = userId;
    }

    public ClaimsPrincipal? Principal { get; }

    public Guid? UserId { get; }

    /// <summary>The module-wide permissions the caller holds.</summary>
    public HashSet<string> ModuleWide { get; } = new(StringComparer.Ordinal);

    /// <summary>The grants read so far, by container.</summary>
    public Dictionary<string, HashSet<string>> Grants { get; } = new(StringComparer.Ordinal);
}
